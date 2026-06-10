using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfSecurityEventRepository(
    MiniAdminDbContext dbContext,
    ISecurityPolicyRepository securityPolicyRepository,
    IDataScopeProvider dataScopeProvider) : ISecurityEventRepository
{
    public async Task<SecurityCenterOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var since24h = now.AddHours(-24);
        var staleBefore = now.AddDays(-Math.Max(policy.StaleUserDays, 1));

        var totalUsers = await dbContext.Users.AsNoTracking().CountAsync(cancellationToken);
        var enabledUsers = await dbContext.Users.AsNoTracking().CountAsync(user => user.IsEnabled, cancellationToken);
        var disabledUsers = totalUsers - enabledUsers;
        var lockedUsers = await dbContext.SecurityEvents
            .AsNoTracking()
            .Where(securityEvent => securityEvent.EventType == "AccountLocked" && securityEvent.CreatedAt >= since24h)
            .Select(securityEvent => securityEvent.UserName)
            .Where(userName => userName != null && userName != "")
            .Distinct()
            .CountAsync(cancellationToken);
        var staleUsers = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.IsEnabled &&
                           !dbContext.LoginLogs.Any(log =>
                               log.UserName == user.UserName &&
                               log.IsSuccess &&
                               log.CreatedAt >= staleBefore))
            .CountAsync(cancellationToken);

        var failedLoginLogs = dbContext.LoginLogs
            .AsNoTracking()
            .Where(log => !log.IsSuccess && log.CreatedAt >= since24h);
        var failedLoginCount = await failedLoginLogs.CountAsync(cancellationToken);
        var failedUserCount = await failedLoginLogs
            .Select(log => log.UserName)
            .Distinct()
            .CountAsync(cancellationToken);
        var failedIpCount = await failedLoginLogs
            .Where(log => log.IpAddress != null && log.IpAddress != "")
            .Select(log => log.IpAddress)
            .Distinct()
            .CountAsync(cancellationToken);

        var permissionChangeCount = await dbContext.SecurityEvents
            .AsNoTracking()
            .CountAsync(securityEvent =>
                securityEvent.CreatedAt >= since24h &&
                (securityEvent.EventType == "RolePermissionChanged" ||
                 securityEvent.EventType == "UserRoleChanged" ||
                 securityEvent.EventType == "MenuPermissionChanged"), cancellationToken);
        var recentHighRiskEvents = await dbContext.SecurityEvents
            .AsNoTracking()
            .Where(securityEvent => securityEvent.Level == "Warning" || securityEvent.Level == "Critical")
            .OrderByDescending(securityEvent => securityEvent.CreatedAt)
            .Take(6)
            .Select(securityEvent => ToDto(securityEvent))
            .ToArrayAsync(cancellationToken);

        var onlineActiveAfter = now.AddMinutes(-Math.Max(policy.OnlineActiveTimeoutMinutes, 1));
        var onlineUsers = await dbContext.OnlineUsers
            .AsNoTracking()
            .CountAsync(user => user.IsOnline && user.LastActiveAt >= onlineActiveAfter, cancellationToken);
        var recentForceLogoutEvents = await dbContext.SecurityEvents
            .AsNoTracking()
            .Where(securityEvent => securityEvent.EventType == "ForceLogout")
            .OrderByDescending(securityEvent => securityEvent.CreatedAt)
            .Take(6)
            .Select(securityEvent => ToDto(securityEvent))
            .ToArrayAsync(cancellationToken);
        var recentEvents = await dbContext.SecurityEvents
            .AsNoTracking()
            .OrderByDescending(securityEvent => securityEvent.CreatedAt)
            .Take(8)
            .Select(securityEvent => ToDto(securityEvent))
            .ToArrayAsync(cancellationToken);

        return new SecurityCenterOverviewDto(
            new SecurityAccountSummaryDto(totalUsers, enabledUsers, disabledUsers, lockedUsers, staleUsers),
            new SecurityLoginSummaryDto(failedLoginCount, failedUserCount, failedIpCount),
            new SecurityPermissionSummaryDto(permissionChangeCount, recentHighRiskEvents),
            new SecuritySessionSummaryDto(onlineUsers, recentForceLogoutEvents),
            recentEvents);
    }

    public async Task<PageResult<SecurityEventDto>> GetEventsAsync(
        SecurityEventListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var eventsQuery = await ApplyDataScopeAsync(
            dbContext.SecurityEvents.AsNoTracking(),
            query.CurrentUserName,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            eventsQuery = eventsQuery.Where(x => x.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Level))
        {
            eventsQuery = eventsQuery.Where(x => x.Level == query.Level);
        }

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            eventsQuery = eventsQuery.Where(x => x.UserName != null && x.UserName.Contains(query.UserName));
        }

        var total = await eventsQuery.CountAsync(cancellationToken);
        var items = await eventsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => ToDto(x))
            .ToArrayAsync(cancellationToken);

        return new PageResult<SecurityEventDto>(items, total);
    }

    public async Task RecordEventAsync(
        SaveSecurityEventRequest request,
        CancellationToken cancellationToken = default)
    {
        dbContext.SecurityEvents.Add(new SecurityEvent
        {
            Id = Guid.NewGuid(),
            EventType = NormalizeRequired(request.EventType),
            Level = NormalizeLevel(request.Level),
            UserId = request.UserId,
            UserName = NormalizeOptional(request.UserName),
            IpAddress = NormalizeOptional(request.IpAddress),
            UserAgent = NormalizeOptional(request.UserAgent),
            Title = NormalizeRequired(request.Title),
            Description = NormalizeRequired(request.Description),
            RelatedEntityType = NormalizeOptional(request.RelatedEntityType),
            RelatedEntityId = NormalizeOptional(request.RelatedEntityId),
            CreatedAt = DateTimeOffset.UtcNow
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<IQueryable<SecurityEvent>> ApplyDataScopeAsync(
        IQueryable<SecurityEvent> eventsQuery,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return eventsQuery;
        }

        if (dataScope.IsDenied || string.IsNullOrWhiteSpace(dataScope.UserName))
        {
            return eventsQuery.Where(x => false);
        }

        if (dataScope.Level is DataScopeLevel.DepartmentAndChildren
            or DataScopeLevel.Department
            or DataScopeLevel.CustomDepartments
            or DataScopeLevel.Mixed)
        {
            return eventsQuery.Where(securityEvent =>
                securityEvent.UserName != null &&
                dbContext.Users.Any(user =>
                    user.UserName == securityEvent.UserName &&
                    user.DepartmentId.HasValue &&
                    dataScope.DepartmentIds.Contains(user.DepartmentId.Value)));
        }

        return eventsQuery.Where(x => x.UserName == dataScope.UserName);
    }

    private static SecurityEventDto ToDto(SecurityEvent securityEvent)
    {
        return new SecurityEventDto(
            securityEvent.Id.ToString(),
            securityEvent.EventType,
            securityEvent.Level,
            securityEvent.UserId?.ToString(),
            securityEvent.UserName,
            securityEvent.IpAddress,
            securityEvent.UserAgent,
            securityEvent.Title,
            securityEvent.Description,
            securityEvent.RelatedEntityType,
            securityEvent.RelatedEntityId,
            securityEvent.CreatedAt);
    }

    private static string NormalizeRequired(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeLevel(string value)
    {
        return value.Trim() switch
        {
            "Critical" => "Critical",
            "Warning" => "Warning",
            "Info" => "Info",
            _ => "Info"
        };
    }
}
