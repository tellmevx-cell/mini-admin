using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Application.Contracts.OnlineUsers;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfOnlineUserRepository(
    MiniAdminDbContext dbContext,
    IUserAuthorizationCache userAuthorizationCache,
    ISecurityPolicyRepository securityPolicyRepository,
    IDataScopeProvider dataScopeProvider) : IOnlineUserRepository
{
    public async Task<PageResult<LoginLogDto>> GetLoginLogsAsync(
        LoginLogListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var logsQuery = await ApplyLoginLogDataScopeAsync(
            dbContext.LoginLogs.AsNoTracking(),
            query.CurrentUserName,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            logsQuery = logsQuery.Where(x => x.UserName.Contains(query.UserName));
        }

        if (query.IsSuccess.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.IsSuccess == query.IsSuccess);
        }

        if (query.StartCreatedAt.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt >= query.StartCreatedAt);
        }

        if (query.EndCreatedAt.HasValue)
        {
            logsQuery = logsQuery.Where(x => x.CreatedAt <= query.EndCreatedAt);
        }

        var total = await logsQuery.CountAsync(cancellationToken);
        var items = await logsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<LoginLogDto>(items, total);
    }

    public async Task<PageResult<OnlineUserDto>> GetOnlineUsersAsync(
        OnlineUserListQuery query,
        CancellationToken cancellationToken = default)
    {
        await MarkExpiredOnlineUsersOfflineAsync(cancellationToken);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var activeAfter = await GetActiveAfterAsync(DateTimeOffset.UtcNow, cancellationToken);
        var usersQuery = dbContext.OnlineUsers
            .AsNoTracking()
            .Where(x => x.IsOnline && x.LastActiveAt >= activeAfter);
        usersQuery = await ApplyOnlineUserDataScopeAsync(usersQuery, query.CurrentUserName, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            usersQuery = usersQuery.Where(x => x.UserName.Contains(query.UserName));
        }

        var total = await usersQuery.CountAsync(cancellationToken);
        var items = await usersQuery
            .OrderByDescending(x => x.LastActiveAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<OnlineUserDto>(items, total);
    }

    public async Task RecordLoginAsync(
        SaveLoginLogRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserName == request.UserName, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        var ipAddress = NormalizeOptional(request.IpAddress);
        var userAgent = NormalizeOptional(request.UserAgent);

        dbContext.LoginLogs.Add(new LoginLog
        {
            Id = Guid.NewGuid(),
            UserId = user?.Id,
            UserName = NormalizeUserName(request.UserName),
            RealName = user?.RealName,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            IsSuccess = request.IsSuccess,
            Message = request.Message,
            CreatedAt = now
        });

        if (request.IsSuccess && user is not null)
        {
            await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);

            var sessionId = request.SessionId.GetValueOrDefault();
            if (sessionId == Guid.Empty)
            {
                sessionId = Guid.NewGuid();
            }

            var oldSameBrowserSessions = await dbContext.OnlineUsers
                .Where(x =>
                    x.UserId == user.Id &&
                    x.IsOnline &&
                    x.IpAddress == ipAddress &&
                    x.UserAgent == userAgent &&
                    x.SessionId != sessionId)
                .ToArrayAsync(cancellationToken);
            foreach (var oldSession in oldSameBrowserSessions)
            {
                oldSession.IsOnline = false;
                oldSession.LastActiveAt = now;
            }

            var session = new OnlineUser
            {
                SessionId = sessionId,
                UserId = user.Id,
                UserName = user.UserName,
                RealName = user.RealName,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                DeviceName = DetectDeviceName(userAgent),
                BrowserName = DetectBrowserName(userAgent),
                LoginAt = now,
                LastActiveAt = now,
                IsOnline = true
            };
            dbContext.OnlineUsers.Add(session);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TouchAsync(
        Guid sessionId,
        Guid userId,
        string userName,
        string? ipAddress,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        if (sessionId == Guid.Empty || userId == Guid.Empty || string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var activeAfter = now.AddMinutes(-Math.Max(policy.OnlineActiveTimeoutMinutes, 1));
        var throttleAfter = now.AddSeconds(-Math.Max(policy.OnlineTouchThrottleSeconds, 5));
        var onlineUser = await dbContext.OnlineUsers
            .SingleOrDefaultAsync(x => x.SessionId == sessionId && x.UserId == userId, cancellationToken);

        if (onlineUser is null)
        {
            return false;
        }

        if (!onlineUser.IsOnline || onlineUser.LastActiveAt < activeAfter)
        {
            if (onlineUser.IsOnline)
            {
                onlineUser.IsOnline = false;
                onlineUser.LastActiveAt = now;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return false;
        }

        if (onlineUser.LastActiveAt >= throttleAfter)
        {
            return true;
        }

        onlineUser.UserName = NormalizeUserName(userName);
        onlineUser.IpAddress = NormalizeOptional(ipAddress);
        onlineUser.UserAgent = NormalizeOptional(userAgent);
        onlineUser.DeviceName = DetectDeviceName(onlineUser.UserAgent);
        onlineUser.BrowserName = DetectBrowserName(onlineUser.UserAgent);
        onlineUser.LastActiveAt = now;
        onlineUser.IsOnline = true;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SignOutAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        var onlineUser = await dbContext.OnlineUsers.SingleOrDefaultAsync(
            x => x.SessionId == sessionId,
            cancellationToken);
        if (onlineUser is null)
        {
            return;
        }

        onlineUser.IsOnline = false;
        onlineUser.LastActiveAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ForceLogoutAsync(
        Guid userId,
        string? currentUserName = null,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        if (!await CanAccessUserAsync(user, currentUserName, cancellationToken))
        {
            return false;
        }

        await LogoutUserAsync(user, cancellationToken);
        return true;
    }

    public async Task<bool> ForceLogoutSessionAsync(
        Guid sessionId,
        string? currentUserName = null,
        CancellationToken cancellationToken = default)
    {
        var onlineUser = await dbContext.OnlineUsers.SingleOrDefaultAsync(
            x => x.SessionId == sessionId,
            cancellationToken);
        if (onlineUser is null)
        {
            return false;
        }

        var targetUser = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == onlineUser.UserId, cancellationToken);
        if (targetUser is null || !await CanAccessUserAsync(targetUser, currentUserName, cancellationToken))
        {
            return false;
        }

        onlineUser.IsOnline = false;
        onlineUser.LastActiveAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }

    private async Task LogoutUserAsync(User user, CancellationToken cancellationToken)
    {
        user.SecurityStamp = CreateSecurityStamp();

        var onlineUsers = await dbContext.OnlineUsers
            .Where(x => x.UserId == user.Id && x.IsOnline)
            .ToArrayAsync(cancellationToken);
        foreach (var onlineUser in onlineUsers)
        {
            onlineUser.IsOnline = false;
            onlineUser.LastActiveAt = DateTimeOffset.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);
    }

    private async Task MarkExpiredOnlineUsersOfflineAsync(CancellationToken cancellationToken)
    {
        var activeAfter = await GetActiveAfterAsync(DateTimeOffset.UtcNow, cancellationToken);
        var expiredUsers = await dbContext.OnlineUsers
            .Where(x => x.IsOnline && x.LastActiveAt < activeAfter)
            .ToArrayAsync(cancellationToken);

        if (expiredUsers.Length == 0)
        {
            return;
        }

        foreach (var user in expiredUsers)
        {
            user.IsOnline = false;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DateTimeOffset> GetActiveAfterAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        return now.AddMinutes(-Math.Max(policy.OnlineActiveTimeoutMinutes, 1));
    }

    private async Task<IQueryable<LoginLog>> ApplyLoginLogDataScopeAsync(
        IQueryable<LoginLog> logsQuery,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return logsQuery;
        }

        if (dataScope.IsDenied || string.IsNullOrWhiteSpace(dataScope.UserName))
        {
            return logsQuery.Where(x => false);
        }

        if (dataScope.Level is DataScopeLevel.DepartmentAndChildren
            or DataScopeLevel.Department
            or DataScopeLevel.CustomDepartments
            or DataScopeLevel.Mixed)
        {
            return logsQuery.Where(log =>
                dbContext.Users.Any(user =>
                    user.UserName == log.UserName &&
                    user.DepartmentId.HasValue &&
                    dataScope.DepartmentIds.Contains(user.DepartmentId.Value)));
        }

        return logsQuery.Where(x => x.UserName == dataScope.UserName);
    }

    private async Task<IQueryable<OnlineUser>> ApplyOnlineUserDataScopeAsync(
        IQueryable<OnlineUser> usersQuery,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return usersQuery;
        }

        if (dataScope.IsDenied || dataScope.UserId is not Guid currentUserId)
        {
            return usersQuery.Where(x => false);
        }

        if (dataScope.Level is DataScopeLevel.DepartmentAndChildren
            or DataScopeLevel.Department
            or DataScopeLevel.CustomDepartments
            or DataScopeLevel.Mixed)
        {
            return usersQuery.Where(x =>
                x.UserId == currentUserId ||
                dbContext.Users.Any(user =>
                    user.Id == x.UserId &&
                    user.DepartmentId.HasValue &&
                    dataScope.DepartmentIds.Contains(user.DepartmentId.Value)));
        }

        return usersQuery.Where(x => x.UserId == currentUserId);
    }

    private async Task<bool> CanAccessUserAsync(
        User user,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return true;
        }

        if (dataScope.IsDenied || dataScope.UserId is not Guid currentUserId)
        {
            return false;
        }

        if (user.Id == currentUserId)
        {
            return true;
        }

        return user.DepartmentId.HasValue &&
               dataScope.DepartmentIds.Contains(user.DepartmentId.Value);
    }

    private static LoginLogDto ToDto(LoginLog loginLog)
    {
        return new LoginLogDto(
            loginLog.Id.ToString(),
            loginLog.UserId?.ToString(),
            loginLog.UserName,
            loginLog.RealName,
            loginLog.IpAddress,
            loginLog.UserAgent,
            loginLog.IsSuccess,
            loginLog.Message,
            loginLog.CreatedAt);
    }

    private static OnlineUserDto ToDto(OnlineUser onlineUser)
    {
        return new OnlineUserDto(
            onlineUser.SessionId.ToString(),
            onlineUser.UserId.ToString(),
            onlineUser.UserName,
            onlineUser.RealName,
            onlineUser.IpAddress,
            onlineUser.UserAgent,
            onlineUser.DeviceName,
            onlineUser.BrowserName,
            onlineUser.LoginAt,
            onlineUser.LastActiveAt);
    }

    private static string NormalizeUserName(string userName)
    {
        return string.IsNullOrWhiteSpace(userName) ? string.Empty : userName.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string CreateSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string DetectDeviceName(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "未知设备";
        }

        if (userAgent.Contains("Mobile", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
            userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase))
        {
            return "移动设备";
        }

        return "桌面设备";
    }

    private static string DetectBrowserName(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "未知浏览器";
        }

        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
        {
            return "Microsoft Edge";
        }

        if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
        {
            return "Chrome";
        }

        if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
        {
            return "Firefox";
        }

        if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
        {
            return "Safari";
        }

        return "其他浏览器";
    }
}
