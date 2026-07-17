using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.TenantResourceQuotas;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfTenantRepository(
    MiniAdminDbContext dbContext,
    IPasswordService passwordService,
    TenantSessionInvalidator tenantSessionInvalidator,
    TenantInitializationTemplateService initializationTemplateService) : ITenantRepository
{
    private const long BytesPerMb = 1024L * 1024L;

    public Task<TenantLookupDto?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeCode(code);
        return dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Code == normalizedCode)
            .Select(x => new TenantLookupDto(x.Id, x.Name, x.Code, x.Status, x.ExpireAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<PageResult<TenantDto>> GetListAsync(
        TenantListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        IQueryable<Tenant> tenantsQuery = dbContext.Tenants
            .AsNoTracking()
            .Include(x => x.Package);

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            tenantsQuery = tenantsQuery.Where(x => x.Code.Contains(query.Code.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            tenantsQuery = tenantsQuery.Where(x => x.Name.Contains(query.Name.Trim()));
        }

        var now = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(query.Status) &&
            Enum.TryParse<TenantStatus>(query.Status, ignoreCase: true, out var status))
        {
            tenantsQuery = status switch
            {
                TenantStatus.Active => tenantsQuery.Where(x =>
                    x.Status == TenantStatus.Active &&
                    (!x.ExpireAt.HasValue || x.ExpireAt.Value > now)),
                TenantStatus.Expired => tenantsQuery.Where(x =>
                    x.Status == TenantStatus.Expired ||
                    (x.Status == TenantStatus.Active &&
                     x.ExpireAt.HasValue &&
                     x.ExpireAt.Value <= now)),
                _ => tenantsQuery.Where(x => x.Status == status)
            };
        }

        var total = await tenantsQuery.CountAsync(cancellationToken);
        var tenants = await tenantsQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var tenantIds = tenants.Select(x => x.Id).ToArray();
        var userUsage = await dbContext.Users
            .AsNoTracking()
            .Where(x => x.TenantId.HasValue && tenantIds.Contains(x.TenantId.Value))
            .GroupBy(x => x.TenantId!.Value)
            .Select(group => new { TenantId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(x => x.TenantId, x => x.Count, cancellationToken);
        var storageUsage = await dbContext.ManagedFiles
            .AsNoTracking()
            .Where(x => x.TenantId.HasValue && tenantIds.Contains(x.TenantId.Value))
            .GroupBy(x => x.TenantId!.Value)
            .Select(group => new { TenantId = group.Key, Bytes = group.Sum(x => x.Size) })
            .ToDictionaryAsync(x => x.TenantId, x => x.Bytes, cancellationToken);
        var quotaNotificationTimes = (await dbContext.TenantResourceQuotaWarnings
                .AsNoTracking()
                .Where(x => tenantIds.Contains(x.TenantId) && x.LastNotifiedAt.HasValue)
                .Select(x => new { x.TenantId, x.LastNotifiedAt })
                .ToArrayAsync(cancellationToken))
            .GroupBy(x => x.TenantId)
            .ToDictionary(
                group => group.Key,
                group => group.Max(x => x.LastNotifiedAt));
        var items = tenants
            .Select(tenant => ToDto(
                tenant,
                userUsage.GetValueOrDefault(tenant.Id),
                storageUsage.GetValueOrDefault(tenant.Id),
                quotaNotificationTimes.GetValueOrDefault(tenant.Id)))
            .ToArray();

        return new PageResult<TenantDto>(items, total);
    }

    public async Task<IReadOnlyList<TenantLoginOptionDto>> GetLoginOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Status == TenantStatus.Active &&
                        (!x.ExpireAt.HasValue || x.ExpireAt.Value > DateTimeOffset.UtcNow))
            .OrderBy(x => x.Code)
            .Select(x => new TenantLoginOptionDto(x.Code, x.Name))
            .ToArrayAsync(cancellationToken);
    }

    public Task<IReadOnlyList<TenantInitializationTemplateDto>> GetInitializationTemplatesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(initializationTemplateService.GetTemplates());
    }

    public async Task<TenantDto> CreateAsync(
        CreateTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default)
    {
        EnsureExpireAtIsInFuture(request.ExpireAt);

        var code = NormalizeCode(request.Code);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("租户编码不能为空");
        }

        var exists = await dbContext.Tenants.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException("租户编码已存在");
        }

        var adminUserName = NormalizeRequired(request.AdminUserName, "租户管理员账号不能为空");
        var adminRealName = NormalizeRequired(request.AdminRealName, "租户管理员姓名不能为空");
        var adminPassword = NormalizeRequired(request.AdminPassword, "租户管理员初始密码不能为空");
        var normalizedAdminUserName = adminUserName.ToLowerInvariant();
        var adminUserExists = await dbContext.Users
            .AnyAsync(
                x => x.UserName.ToLower() == normalizedAdminUserName,
                cancellationToken);
        if (adminUserExists)
        {
            throw new InvalidOperationException("租户管理员账号已存在");
        }

        var tenantAdminRoleId = await EnsureTenantAdminRoleAsync(cancellationToken);
        await EnsureTenantAdminRoleMenusAsync(tenantAdminRoleId, cancellationToken);
        var packageId = await ResolvePackageIdAsync(request.PackageId, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var tenantId = Guid.NewGuid();
        var adminUserId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Code = code,
            Name = NormalizeRequired(request.Name, "租户名称不能为空"),
            Status = TenantStatus.Active,
            PackageId = packageId,
            InitializationTemplateCode = NormalizeTemplateCode(request.InitializationTemplateCode),
            InitializationStatus = "Pending",
            ContactName = NormalizeOptional(request.ContactName),
            ContactPhone = NormalizeOptional(request.ContactPhone),
            ContactEmail = NormalizeOptional(request.ContactEmail),
            ExpireAt = request.ExpireAt,
            Remark = NormalizeOptional(request.Remark),
            CreatedAt = now,
            UpdatedAt = now
        };
        var adminUser = new User
        {
            Id = adminUserId,
            TenantId = tenantId,
            UserName = adminUserName,
            RealName = adminRealName,
            Email = NormalizeOptional(request.AdminEmail),
            PasswordHash = passwordService.HashPassword(adminPassword),
            SecurityStamp = CreateSecurityStamp(),
            IsEnabled = true,
            UserRoles =
            [
                new UserRole
                {
                    UserId = adminUserId,
                    RoleId = tenantAdminRoleId
                }
            ]
        };

        dbContext.Tenants.Add(tenant);
        dbContext.Users.Add(adminUser);
        AddLifecycleRecord(
            tenant.Id,
            TenantLifecycleEventTypes.Created,
            actor,
            null,
            TenantStatus.Active,
            null,
            tenant.ExpireAt,
            null,
            tenant.PackageId,
            "租户已创建");
        await initializationTemplateService.InitializeAsync(
            tenant,
            adminUser,
            request.InitializationTemplateCode,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(tenant.Id, cancellationToken);
    }

    public async Task<TenantDto?> UpdateAsync(
        Guid id,
        UpdateTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants
            .Include(x => x.Package)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        EnsureExpireAtIsInFuture(request.ExpireAt);

        var oldPackageId = tenant.PackageId;
        var oldExpireAt = tenant.ExpireAt;
        var oldEffectiveStatus = GetEffectiveStatus(tenant);
        tenant.Name = NormalizeRequired(request.Name, "租户名称不能为空");
        tenant.PackageId = await ResolvePackageIdAsync(request.PackageId, cancellationToken);
        tenant.ContactName = NormalizeOptional(request.ContactName);
        tenant.ContactPhone = NormalizeOptional(request.ContactPhone);
        tenant.ContactEmail = NormalizeOptional(request.ContactEmail);
        tenant.ExpireAt = request.ExpireAt;
        tenant.Remark = NormalizeOptional(request.Remark);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        var shouldInvalidateSessions = oldPackageId != tenant.PackageId;
        if (oldPackageId != tenant.PackageId)
        {
            AddLifecycleRecord(
                tenant.Id,
                TenantLifecycleEventTypes.PackageChanged,
                actor,
                oldEffectiveStatus,
                GetEffectiveStatus(tenant),
                oldExpireAt,
                tenant.ExpireAt,
                oldPackageId,
                tenant.PackageId,
                "租户套餐已调整");
        }

        if (oldExpireAt != tenant.ExpireAt)
        {
            var eventType = tenant.ExpireAt.HasValue &&
                            (!oldExpireAt.HasValue || tenant.ExpireAt.Value > oldExpireAt.Value)
                ? TenantLifecycleEventTypes.Renewed
                : TenantLifecycleEventTypes.ExpirationChanged;
            AddLifecycleRecord(
                tenant.Id,
                eventType,
                actor,
                oldEffectiveStatus,
                GetEffectiveStatus(tenant),
                oldExpireAt,
                tenant.ExpireAt,
                oldPackageId,
                tenant.PackageId,
                DescribeExpireAtChange(oldExpireAt, tenant.ExpireAt));

            if (oldEffectiveStatus == TenantStatus.Expired &&
                GetEffectiveStatus(tenant) == TenantStatus.Active)
            {
                shouldInvalidateSessions = true;
            }
        }

        if (shouldInvalidateSessions)
        {
            await tenantSessionInvalidator.InvalidateAsync(tenant.Id, cancellationToken);
        }

        if (oldPackageId == tenant.PackageId && oldExpireAt == tenant.ExpireAt)
        {
            AddLifecycleRecord(
                tenant.Id,
                TenantLifecycleEventTypes.Updated,
                actor,
                oldEffectiveStatus,
                GetEffectiveStatus(tenant),
                oldExpireAt,
                tenant.ExpireAt,
                oldPackageId,
                tenant.PackageId,
                "租户资料已更新");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (oldPackageId != tenant.PackageId)
        {
            tenant.Package = tenant.PackageId.HasValue
                ? await dbContext.TenantPackages
                    .AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == tenant.PackageId.Value, cancellationToken)
                : null;
        }

        return await BuildDtoAsync(tenant.Id, cancellationToken);
    }

    public async Task<TenantDto?> SetStatusAsync(
        Guid id,
        string status,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default)
    {
        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var oldStatus = GetEffectiveStatus(tenant);
        var newStatus = ParseStatus(status);
        if (newStatus == TenantStatus.Active &&
            tenant.ExpireAt.HasValue &&
            tenant.ExpireAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("租户已过期，请先续期后再启用");
        }

        if (oldStatus == newStatus && tenant.Status == newStatus)
        {
            return await BuildDtoAsync(tenant.Id, cancellationToken);
        }

        tenant.Status = newStatus;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        if (tenant.Status != TenantStatus.Active)
        {
            await tenantSessionInvalidator.InvalidateAsync(tenant.Id, cancellationToken);
        }

        AddLifecycleRecord(
            tenant.Id,
            tenant.Status == TenantStatus.Active
                ? TenantLifecycleEventTypes.Enabled
                : TenantLifecycleEventTypes.Disabled,
            actor,
            oldStatus,
            tenant.Status,
            tenant.ExpireAt,
            tenant.ExpireAt,
            tenant.PackageId,
            tenant.PackageId,
            tenant.Status == TenantStatus.Active ? "租户已启用" : "租户已停用");

        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildDtoAsync(tenant.Id, cancellationToken);
    }

    public async Task<TenantDto?> RenewAsync(
        Guid id,
        RenewTenantRequest request,
        TenantOperationActor actor,
        CancellationToken cancellationToken = default)
    {
        EnsureExpireAtIsInFuture(request.ExpireAt);
        var tenant = await dbContext.Tenants
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var oldStatus = GetEffectiveStatus(tenant);
        var oldExpireAt = tenant.ExpireAt;
        tenant.ExpireAt = request.ExpireAt;
        if (request.Reactivate)
        {
            tenant.Status = TenantStatus.Active;
        }
        if (!string.IsNullOrWhiteSpace(request.Remark))
        {
            tenant.Remark = request.Remark.Trim();
        }
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await tenantSessionInvalidator.InvalidateAsync(tenant.Id, cancellationToken);

        AddLifecycleRecord(
            tenant.Id,
            TenantLifecycleEventTypes.Renewed,
            actor,
            oldStatus,
            GetEffectiveStatus(tenant),
            oldExpireAt,
            tenant.ExpireAt,
            tenant.PackageId,
            tenant.PackageId,
            DescribeExpireAtChange(oldExpireAt, tenant.ExpireAt));

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildDtoAsync(tenant.Id, cancellationToken);
    }

    public Task<TenantLookupDto?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new TenantLookupDto(x.Id, x.Name, x.Code, x.Status, x.ExpireAt))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeCode(string code)
    {
        return string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToLowerInvariant();
    }

    private async Task<Guid> EnsureTenantAdminRoleAsync(CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.SingleOrDefaultAsync(
            x => x.Code == "tenant-admin",
            cancellationToken);
        if (role is not null)
        {
            return role.Id;
        }

        var roleId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "tenant-admin",
            Name = "Tenant Administrator",
            DataScope = "all",
            IsEnabled = true
        });

        return roleId;
    }

    private async Task EnsureTenantAdminRoleMenusAsync(
        Guid roleId,
        CancellationToken cancellationToken)
    {
        foreach (var menuId in GetTenantAdminMenuIds())
        {
            var exists = await dbContext.RoleMenus.AnyAsync(
                x => x.RoleId == roleId && x.MenuId == menuId,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = roleId,
                MenuId = menuId
            });
        }
    }

    private static IReadOnlyList<Guid> GetTenantAdminMenuIds()
    {
        return
        [
            MiniAdminSeedIds.DashboardMenuId,
            MiniAdminSeedIds.WorkspaceMenuId,
            MiniAdminSeedIds.SystemMenuId,
            MiniAdminSeedIds.UserManagementMenuId,
            MiniAdminSeedIds.UserQueryPermissionId,
            MiniAdminSeedIds.UserCreatePermissionId,
            MiniAdminSeedIds.UserUpdatePermissionId,
            MiniAdminSeedIds.UserDeletePermissionId,
            MiniAdminSeedIds.RoleManagementMenuId,
            MiniAdminSeedIds.RoleQueryPermissionId,
            MiniAdminSeedIds.RoleCreatePermissionId,
            MiniAdminSeedIds.RoleUpdatePermissionId,
            MiniAdminSeedIds.RoleDeletePermissionId,
            MiniAdminSeedIds.RoleAssignPermissionId,
            MiniAdminSeedIds.DepartmentManagementMenuId,
            MiniAdminSeedIds.DepartmentQueryPermissionId,
            MiniAdminSeedIds.DepartmentCreatePermissionId,
            MiniAdminSeedIds.DepartmentUpdatePermissionId,
            MiniAdminSeedIds.DepartmentDeletePermissionId,
            MiniAdminSeedIds.PositionManagementMenuId,
            MiniAdminSeedIds.PositionQueryPermissionId,
            MiniAdminSeedIds.PositionCreatePermissionId,
            MiniAdminSeedIds.PositionUpdatePermissionId,
            MiniAdminSeedIds.PositionDeletePermissionId
        ];
    }

    private async Task<Guid?> ResolvePackageIdAsync(
        Guid? requestedPackageId,
        CancellationToken cancellationToken)
    {
        var packageId = requestedPackageId ?? MiniAdminSeedIds.DefaultTenantPackageId;
        var exists = await dbContext.TenantPackages
            .AnyAsync(x => x.Id == packageId, cancellationToken);
        if (!exists)
        {
            return null;
        }

        return packageId;
    }

    private async Task<TenantDto> BuildDtoAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await dbContext.Tenants
            .AsNoTracking()
            .Include(x => x.Package)
            .SingleAsync(x => x.Id == tenantId, cancellationToken);
        var usedUsers = await dbContext.Users
            .AsNoTracking()
            .CountAsync(x => x.TenantId == tenantId, cancellationToken);
        var usedStorageBytes = await dbContext.ManagedFiles
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .SumAsync(x => (long?)x.Size, cancellationToken) ?? 0;
        var quotaLastNotifiedAt = await dbContext.TenantResourceQuotaWarnings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .MaxAsync(x => (DateTimeOffset?)x.LastNotifiedAt, cancellationToken);

        return ToDto(tenant, usedUsers, usedStorageBytes, quotaLastNotifiedAt);
    }

    private static TenantDto ToDto(
        Tenant tenant,
        int usedUsers,
        long usedStorageBytes,
        DateTimeOffset? quotaLastNotifiedAt = null)
    {
        var status = GetEffectiveStatus(tenant);

        return new TenantDto(
            tenant.Id.ToString(),
            tenant.Code,
            tenant.Name,
            status.ToString(),
            tenant.PackageId?.ToString(),
            tenant.Package?.Name,
            usedUsers,
            Math.Max(tenant.Package?.MaxUsers ?? 0, 0),
            usedStorageBytes,
            checked((long)Math.Max(tenant.Package?.MaxStorageMb ?? 0, 0) * BytesPerMb),
            TenantQuotaStatuses.Evaluate(usedUsers, Math.Max(tenant.Package?.MaxUsers ?? 0, 0)),
            TenantQuotaStatuses.Evaluate(
                usedStorageBytes,
                checked((long)Math.Max(tenant.Package?.MaxStorageMb ?? 0, 0) * BytesPerMb)),
            quotaLastNotifiedAt,
            tenant.InitializationTemplateCode,
            tenant.InitializationStatus,
            tenant.InitializedAt,
            tenant.InitializationError,
            tenant.ContactName,
            tenant.ContactPhone,
            tenant.ContactEmail,
            tenant.ExpireAt,
            tenant.Remark,
            tenant.CreatedAt,
            tenant.UpdatedAt);
    }

    private static TenantStatus GetEffectiveStatus(Tenant tenant)
    {
        return tenant.Status == TenantStatus.Active &&
               tenant.ExpireAt.HasValue &&
               tenant.ExpireAt.Value <= DateTimeOffset.UtcNow
            ? TenantStatus.Expired
            : tenant.Status;
    }

    private static void EnsureExpireAtIsInFuture(DateTimeOffset? expireAt)
    {
        if (expireAt.HasValue && expireAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("租户到期时间必须晚于当前时间");
        }
    }

    private static TenantStatus ParseStatus(string status)
    {
        return Enum.TryParse<TenantStatus>(status, ignoreCase: true, out var value)
            ? value
            : TenantStatus.Active;
    }

    private static string NormalizeRequired(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeTemplateCode(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? TenantInitializationTemplateService.StandardTemplateCode
            : value.Trim().ToLowerInvariant();
    }

    private static string CreateSecurityStamp()
    {
        return Guid.NewGuid().ToString("N");
    }

    private void AddLifecycleRecord(
        Guid tenantId,
        string eventType,
        TenantOperationActor actor,
        TenantStatus? fromStatus,
        TenantStatus? toStatus,
        DateTimeOffset? previousExpireAt,
        DateTimeOffset? newExpireAt,
        Guid? previousPackageId,
        Guid? newPackageId,
        string description)
    {
        dbContext.TenantLifecycleRecords.Add(new TenantLifecycleRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            EventType = eventType,
            Source = actor.Source,
            OperatorUserId = actor.UserId,
            OperatorUserName = NormalizeOptional(actor.UserName),
            FromStatus = fromStatus?.ToString(),
            ToStatus = toStatus?.ToString(),
            PreviousExpireAt = previousExpireAt,
            NewExpireAt = newExpireAt,
            PreviousPackageId = previousPackageId,
            NewPackageId = newPackageId,
            Description = description,
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    private static string DescribeExpireAtChange(
        DateTimeOffset? previousExpireAt,
        DateTimeOffset? newExpireAt)
    {
        var previous = previousExpireAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "不限制";
        var next = newExpireAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "不限制";
        return $"到期时间由 {previous} 调整为 {next}";
    }
}
