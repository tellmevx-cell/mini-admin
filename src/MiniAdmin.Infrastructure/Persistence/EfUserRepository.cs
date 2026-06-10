using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfUserRepository(
    MiniAdminDbContext dbContext,
    IPasswordService passwordService,
    IDataScopeProvider dataScopeProvider,
    IUserAuthorizationCache userAuthorizationCache,
    ISecurityEventRepository securityEventRepository,
    ICurrentTenant currentTenant) : IUserRepository
{
    public async Task<CurrentUserDto> GetByUserNameAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleAsync(x => x.UserName == userName && x.IsEnabled, cancellationToken);

        var roles = user.UserRoles
            .Select(x => x.Role)
            .Where(x => x.IsEnabled)
            .Select(x => x.Code)
            .Distinct()
            .Order()
            .ToArray();

        return new CurrentUserDto(
            user.Id.ToString(),
            user.UserName,
            user.RealName,
            user.DepartmentId?.ToString(),
            user.Department?.Name,
            user.PositionId?.ToString(),
            user.Position?.Name,
            roles);
    }

    public async Task<PageResult<UserListItemDto>> GetListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var usersQuery = ApplyTenantScope(dbContext.Users.AsNoTracking());
        usersQuery = await ApplyDataScopeAsync(usersQuery, query.CurrentUserName, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            usersQuery = usersQuery.Where(x => x.UserName.Contains(query.UserName));
        }

        if (query.DepartmentId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.DepartmentId == query.DepartmentId);
        }

        if (query.PositionId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.PositionId == query.PositionId);
        }

        var total = await usersQuery.CountAsync(cancellationToken);
        var users = await usersQuery
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = users
            .Select(user => new UserListItemDto(
                user.Id.ToString(),
                user.UserName,
                user.RealName,
                user.Email,
                user.DepartmentId?.ToString(),
                user.Department?.Name,
                user.PositionId?.ToString(),
                user.Position?.Name,
                user.UserRoles
                    .Select(x => x.Role.Code)
                    .Distinct()
                    .Order()
                    .ToArray(),
                user.IsEnabled ? 1 : 0))
            .ToArray();

        return new PageResult<UserListItemDto>(items, total);
    }

    public async Task<IReadOnlyList<UserListItemDto>> GetExportListAsync(
        UserListQuery query,
        CancellationToken cancellationToken = default)
    {
        var usersQuery = ApplyTenantScope(dbContext.Users.AsNoTracking());
        usersQuery = await ApplyDataScopeAsync(usersQuery, query.CurrentUserName, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.UserName))
        {
            usersQuery = usersQuery.Where(x => x.UserName.Contains(query.UserName));
        }

        if (query.DepartmentId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.DepartmentId == query.DepartmentId);
        }

        if (query.PositionId.HasValue)
        {
            usersQuery = usersQuery.Where(x => x.PositionId == query.PositionId);
        }

        var users = await usersQuery
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .OrderBy(x => x.UserName)
            .Take(5000)
            .ToListAsync(cancellationToken);

        return users.Select(ToDto).ToArray();
    }

    public async Task<UserImportResultDto> ValidateImportAsync(
        IReadOnlyList<UserImportRowDto> rows,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateImportRowsAsync(rows, currentUserName, cancellationToken);
        return new UserImportResultDto(validation.ValidRows.Count, validation.Errors);
    }

    public async Task<UserImportResultDto> ImportAsync(
        IReadOnlyList<UserImportRowDto> rows,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateImportRowsAsync(rows, currentUserName, cancellationToken);
        var createdUsers = validation.ValidRows
            .Select(validRow =>
            {
                var userId = Guid.NewGuid();
                return new User
                {
                    Id = userId,
                    TenantId = currentTenant.TenantId,
                    UserName = validRow.Row.UserName.Trim(),
                    RealName = validRow.Row.RealName.Trim(),
                    PasswordHash = passwordService.HashPassword(validRow.Row.Password),
                    DepartmentId = validRow.DepartmentId,
                    PositionId = validRow.PositionId,
                    SecurityStamp = CreateSecurityStamp(),
                    IsEnabled = validRow.Row.IsEnabled,
                    UserRoles = validRow.RoleIds
                        .Distinct()
                        .Select(roleId => new UserRole
                        {
                            UserId = userId,
                            RoleId = roleId
                        })
                        .ToList()
                };
            })
            .ToArray();

        if (createdUsers.Length > 0)
        {
            dbContext.Users.AddRange(createdUsers);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UserImportResultDto(createdUsers.Length, validation.Errors);
    }

    private async Task<UserImportValidationResult> ValidateImportRowsAsync(
        IReadOnlyList<UserImportRowDto> rows,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var errors = new List<UserImportErrorDto>();
        if (rows.Count == 0)
        {
            return new UserImportValidationResult([], errors);
        }

        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        var departmentCodes = rows
            .Select(x => x.DepartmentCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var positionCodes = rows
            .Select(x => x.PositionCode)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var roleCodes = rows
            .SelectMany(x => x.RoleCodes)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var userNames = rows
            .Select(x => x.UserName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var departmentItems = (await ApplyTenantScope(dbContext.Departments.AsNoTracking())
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken))
            .Where(x => departmentCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var departments = departmentItems.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var positionItems = (await ApplyTenantScope(dbContext.Positions.AsNoTracking())
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken))
            .Where(x => positionCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var positions = positionItems.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var roleItems = (await ApplyTenantScope(dbContext.Roles.AsNoTracking())
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken))
            .Where(x => roleCodes.Contains(x.Code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var roles = roleItems.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var existingUserNames = (await dbContext.Users
            .AsNoTracking()
            .Select(x => x.UserName)
            .ToArrayAsync(cancellationToken))
            .Where(x => userNames.Contains(x, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var existingUserNameSet = new HashSet<string>(existingUserNames, StringComparer.OrdinalIgnoreCase);
        var importedUserNameSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var validRows = new List<ValidatedUserImportRow>();

        foreach (var row in rows)
        {
            if (!importedUserNameSet.Add(row.UserName))
            {
                errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, "导入文件中用户名重复."));
                continue;
            }

            if (existingUserNameSet.Contains(row.UserName))
            {
                errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, "用户名已存在."));
                continue;
            }

            Guid? departmentId = null;
            if (!string.IsNullOrWhiteSpace(row.DepartmentCode))
            {
                if (!departments.TryGetValue(row.DepartmentCode, out var department))
                {
                    errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, "部门编码不存在或已停用."));
                    continue;
                }

                if (!CanAccessDepartment(dataScope, department.Id))
                {
                    errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, "没有权限导入到该部门."));
                    continue;
                }

                departmentId = department.Id;
            }

            Guid? positionId = null;
            if (!string.IsNullOrWhiteSpace(row.PositionCode))
            {
                if (!positions.TryGetValue(row.PositionCode, out var position))
                {
                    errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, "岗位编码不存在或已停用."));
                    continue;
                }

                positionId = position.Id;
            }

            var userRoleIds = new List<Guid>();
            foreach (var roleCode in row.RoleCodes)
            {
                if (!roles.TryGetValue(roleCode, out var role))
                {
                    errors.Add(new UserImportErrorDto(row.RowNumber, row.UserName, $"角色编码 {roleCode} 不存在或已停用."));
                    userRoleIds.Clear();
                    break;
                }

                userRoleIds.Add(role.Id);
            }

            if (row.RoleCodes.Count > 0 && userRoleIds.Count == 0)
            {
                continue;
            }

            validRows.Add(new ValidatedUserImportRow(
                row,
                departmentId,
                positionId,
                userRoleIds));
        }

        return new UserImportValidationResult(validRows, errors);
    }

    public async Task<UserListItemDto> CreateAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds, cancellationToken);
        await EnsureValidTenantReferencesAsync(
            request.DepartmentId,
            request.PositionId,
            request.RoleIds,
            validRoleIds,
            cancellationToken);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            TenantId = currentTenant.TenantId,
            UserName = request.UserName.Trim(),
            RealName = request.RealName.Trim(),
            Email = NormalizeEmail(request.Email),
            PasswordHash = passwordService.HashPassword(request.Password),
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            SecurityStamp = CreateSecurityStamp(),
            IsEnabled = request.IsEnabled,
            UserRoles = validRoleIds
                .Select(roleId => new UserRole
                {
                    UserId = userId,
                    RoleId = roleId
                })
                .ToList()
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsListItemAsync(user.Id, cancellationToken))!;
    }

    public async Task<UserListItemDto?> UpdateAsync(
        Guid id,
        string? currentUserName,
        UpdateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (!CanAccessTenant(user) ||
            !CanAccessUser(dataScope, user) ||
            !CanAccessDepartment(dataScope, request.DepartmentId))
        {
            return null;
        }

        user.RealName = request.RealName.Trim();
        user.Email = NormalizeEmail(request.Email);
        user.DepartmentId = request.DepartmentId;
        user.PositionId = request.PositionId;
        var wasEnabled = user.IsEnabled;
        var previousRoleIds = user.UserRoles.Select(userRole => userRole.RoleId).ToHashSet();
        var validRoleIds = await GetValidRoleIdsAsync(request.RoleIds, cancellationToken);
        await EnsureValidTenantReferencesAsync(
            request.DepartmentId,
            request.PositionId,
            request.RoleIds,
            validRoleIds,
            cancellationToken);
        if (await WouldRemoveLastAdministratorAsync(user, validRoleIds, request.IsEnabled, cancellationToken))
        {
            throw new UserOperationException("至少保留一个可用管理员.");
        }

        user.IsEnabled = request.IsEnabled;
        user.SecurityStamp = CreateSecurityStamp();
        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.PasswordHash = passwordService.HashPassword(request.Password);
        }

        dbContext.UserRoles.RemoveRange(user.UserRoles);
        user.UserRoles = validRoleIds
            .Select(roleId => new UserRole
            {
                UserId = user.Id,
                RoleId = roleId
            })
            .ToList();
        if (wasEnabled && !user.IsEnabled)
        {
            await MarkUserOfflineAsync(user.Id, cancellationToken);
            await securityEventRepository.RecordEventAsync(
                new SaveSecurityEventRequest(
                    "UserDisabled",
                    "Warning",
                    "用户被禁用",
                    $"用户 {user.UserName} 已被禁用，旧 token 已失效.",
                    UserId: user.Id,
                    UserName: user.UserName,
                    RelatedEntityType: "User",
                    RelatedEntityId: user.Id.ToString()),
                cancellationToken);
        }

        if (!previousRoleIds.SetEquals(validRoleIds))
        {
            await securityEventRepository.RecordEventAsync(
                new SaveSecurityEventRequest(
                    "UserRoleChanged",
                    "Warning",
                    "用户角色变更",
                    $"用户 {user.UserName} 的角色授权已变更，旧 token 已失效.",
                    UserId: user.Id,
                    UserName: user.UserName,
                    RelatedEntityType: "User",
                    RelatedEntityId: user.Id.ToString()),
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);

        return await GetByIdAsListItemAsync(id, cancellationToken);
    }

    public async Task<PasswordOperationResult> ChangePasswordAsync(
        string userName,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .SingleOrDefaultAsync(x => x.UserName == userName && x.IsEnabled, cancellationToken);
        if (user is null)
        {
            return PasswordOperationResult.UserNotFound();
        }

        if (!passwordService.VerifyPassword(user.PasswordHash, oldPassword))
        {
            return PasswordOperationResult.OldPasswordIncorrect();
        }

        user.PasswordHash = passwordService.HashPassword(newPassword);
        user.SecurityStamp = CreateSecurityStamp();
        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);

        return PasswordOperationResult.Succeeded();
    }

    public async Task<PasswordOperationResult> ResetPasswordAsync(
        Guid id,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return PasswordOperationResult.UserNotFound();
        }

        user.PasswordHash = passwordService.HashPassword(newPassword);
        user.SecurityStamp = CreateSecurityStamp();
        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);

        return PasswordOperationResult.Succeeded();
    }

    public async Task<DeleteUserResult> DeleteAsync(
        Guid id,
        string? currentUserName,
        CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users
            .Include(x => x.UserRoles)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user is null)
        {
            return DeleteUserResult.NotFound;
        }

        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.UserId == user.Id)
        {
            return DeleteUserResult.CurrentUser;
        }

        if (!CanAccessTenant(user) || !CanAccessUser(dataScope, user))
        {
            return DeleteUserResult.Forbidden;
        }

        if (user.UserName == "admin")
        {
            return DeleteUserResult.BuiltInAdmin;
        }

        if (await IsLastEnabledAdministratorAsync(user, cancellationToken))
        {
            return DeleteUserResult.LastAdministrator;
        }

        dbContext.UserRoles.RemoveRange(user.UserRoles);
        dbContext.Users.Remove(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        await userAuthorizationCache.RemoveUserAsync(user.Id, user.UserName, cancellationToken);

        return DeleteUserResult.Deleted;
    }

    private async Task<bool> WouldRemoveLastAdministratorAsync(
        User user,
        IReadOnlyList<Guid> nextRoleIds,
        bool nextEnabled,
        CancellationToken cancellationToken)
    {
        var adminRoleId = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.Code == "admin")
            .Select(role => (Guid?)role.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (adminRoleId is null)
        {
            return false;
        }

        var currentlyAdmin = user.IsEnabled && user.UserRoles.Any(userRole => userRole.RoleId == adminRoleId.Value);
        var willRemainAdmin = nextEnabled && nextRoleIds.Contains(adminRoleId.Value);
        if (!currentlyAdmin || willRemainAdmin)
        {
            return false;
        }

        return !await dbContext.Users
            .AsNoTracking()
            .AnyAsync(otherUser =>
                otherUser.Id != user.Id &&
                otherUser.IsEnabled &&
                otherUser.UserRoles.Any(userRole => userRole.RoleId == adminRoleId.Value),
                cancellationToken);
    }

    private async Task<bool> IsLastEnabledAdministratorAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var roleIds = user.UserRoles.Select(userRole => userRole.RoleId).ToArray();
        return await WouldRemoveLastAdministratorAsync(user, roleIds, false, cancellationToken);
    }

    private async Task MarkUserOfflineAsync(Guid userId, CancellationToken cancellationToken)
    {
        var onlineUsers = await dbContext.OnlineUsers
            .Where(onlineUser => onlineUser.UserId == userId && onlineUser.IsOnline)
            .ToArrayAsync(cancellationToken);
        foreach (var onlineUser in onlineUsers)
        {
            onlineUser.IsOnline = false;
            onlineUser.LastActiveAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<IReadOnlyList<Guid>> GetValidRoleIdsAsync(
        IReadOnlyList<Guid> roleIds,
        CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .Where(x => roleIds.Contains(x.Id) && x.IsEnabled)
            .Where(x => currentTenant.IsPlatform
                ? x.TenantId == null
                : x.TenantId == currentTenant.TenantId)
            .Select(x => x.Id)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<UserListItemDto?> GetByIdAsListItemAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Include(x => x.Department)
            .Include(x => x.Position)
            .Include(x => x.UserRoles)
            .ThenInclude(x => x.Role)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

        return user is null || !CanAccessTenant(user) ? null : ToDto(user);
    }

    private static UserListItemDto ToDto(User user)
    {
        return new UserListItemDto(
            user.Id.ToString(),
            user.UserName,
            user.RealName,
            user.Email,
            user.DepartmentId?.ToString(),
            user.Department?.Name,
            user.PositionId?.ToString(),
            user.Position?.Name,
            user.UserRoles
                .Select(x => x.Role.Code)
                .Distinct()
                .Order()
                .ToArray(),
            user.IsEnabled ? 1 : 0);
    }

    private static string? NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? null : email.Trim();
    }

    private async Task<IQueryable<User>> ApplyDataScopeAsync(
        IQueryable<User> usersQuery,
        string? currentUserName,
        CancellationToken cancellationToken)
    {
        var dataScope = await dataScopeProvider.GetAsync(currentUserName, cancellationToken);
        if (dataScope.IsUnrestricted)
        {
            return usersQuery;
        }

        if (dataScope.IsDenied || dataScope.UserId is not Guid userId)
        {
            return usersQuery.Where(x => false);
        }

        if (dataScope.Level == DataScopeLevel.DepartmentAndChildren)
        {
            return usersQuery.Where(x =>
                x.Id == userId ||
                (x.DepartmentId.HasValue && dataScope.DepartmentIds.Contains(x.DepartmentId.Value)));
        }

        if (dataScope.Level == DataScopeLevel.Department && dataScope.DepartmentId is Guid departmentId)
        {
            return usersQuery.Where(x => x.Id == userId || x.DepartmentId == departmentId);
        }

        return usersQuery.Where(x => x.Id == userId);
    }

    private IQueryable<User> ApplyTenantScope(IQueryable<User> usersQuery)
    {
        return currentTenant.IsTenant
            ? usersQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : usersQuery;
    }

    private IQueryable<Role> ApplyTenantScope(IQueryable<Role> rolesQuery)
    {
        return currentTenant.IsTenant
            ? rolesQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : rolesQuery.Where(x => x.TenantId == null);
    }

    private IQueryable<Department> ApplyTenantScope(IQueryable<Department> departmentsQuery)
    {
        return currentTenant.IsTenant
            ? departmentsQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : departmentsQuery.Where(x => x.TenantId == null);
    }

    private IQueryable<Position> ApplyTenantScope(IQueryable<Position> positionsQuery)
    {
        return currentTenant.IsTenant
            ? positionsQuery.Where(x => x.TenantId == currentTenant.TenantId)
            : positionsQuery.Where(x => x.TenantId == null);
    }

    private bool CanAccessTenant(User user)
    {
        return currentTenant.IsPlatform || user.TenantId == currentTenant.TenantId;
    }

    private async Task EnsureValidTenantReferencesAsync(
        Guid? departmentId,
        Guid? positionId,
        IReadOnlyList<Guid> requestedRoleIds,
        IReadOnlyList<Guid> validRoleIds,
        CancellationToken cancellationToken)
    {
        if (departmentId.HasValue &&
            !await ApplyTenantScope(dbContext.Departments.AsNoTracking())
                .AnyAsync(x => x.Id == departmentId.Value, cancellationToken))
        {
            throw new UserOperationException("部门不存在或不属于当前租户.");
        }

        if (positionId.HasValue &&
            !await ApplyTenantScope(dbContext.Positions.AsNoTracking())
                .AnyAsync(x => x.Id == positionId.Value, cancellationToken))
        {
            throw new UserOperationException("岗位不存在或不属于当前租户.");
        }

        if (requestedRoleIds.Distinct().Count() != validRoleIds.Count)
        {
            throw new UserOperationException("角色不存在或不属于当前租户.");
        }
    }

    private static bool CanAccessUser(DataScopeContext dataScope, User user)
    {
        if (dataScope.IsUnrestricted)
        {
            return true;
        }

        if (dataScope.IsDenied || dataScope.UserId is not Guid userId)
        {
            return false;
        }

        if (user.Id == userId)
        {
            return true;
        }

        if (dataScope.Level == DataScopeLevel.DepartmentAndChildren)
        {
            return user.DepartmentId is Guid departmentId &&
                dataScope.DepartmentIds.Contains(departmentId);
        }

        if (dataScope.Level == DataScopeLevel.Department)
        {
            return user.DepartmentId is Guid departmentId &&
                dataScope.DepartmentId == departmentId;
        }

        return false;
    }

    private static bool CanAccessDepartment(DataScopeContext dataScope, Guid? departmentId)
    {
        if (dataScope.IsUnrestricted || departmentId is null)
        {
            return true;
        }

        if (dataScope.IsDenied)
        {
            return false;
        }

        if (dataScope.Level == DataScopeLevel.DepartmentAndChildren)
        {
            return dataScope.DepartmentIds.Contains(departmentId.Value);
        }

        if (dataScope.Level == DataScopeLevel.Department)
        {
            return dataScope.DepartmentId == departmentId;
        }

        return false;
    }

    private static string CreateSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }

    private sealed record UserImportValidationResult(
        IReadOnlyList<ValidatedUserImportRow> ValidRows,
        IReadOnlyList<UserImportErrorDto> Errors);

    private sealed record ValidatedUserImportRow(
        UserImportRowDto Row,
        Guid? DepartmentId,
        Guid? PositionId,
        IReadOnlyList<Guid> RoleIds);
}
