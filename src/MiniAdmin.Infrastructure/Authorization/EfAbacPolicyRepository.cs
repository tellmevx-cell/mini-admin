using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Authorization;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Platform.Authorization;
using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Infrastructure.Authorization;

public sealed class EfAbacPolicyRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant,
    IPlatformCache platformCache) : IAbacPolicyRepository, IAbacPolicyProvider
{
    public async Task<IReadOnlyList<AbacPolicyDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AbacPolicies.AsNoTracking();
        if (currentTenant.TenantId is Guid tenantId)
        {
            query = query.Where(policy => policy.TenantId == null || policy.TenantId == tenantId);
        }

        return await query
            .OrderBy(policy => policy.TenantId)
            .ThenByDescending(policy => policy.Priority)
            .ThenBy(policy => policy.Name)
            .Select(policy => ToDto(policy))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<AbacPolicyDto> CreateAsync(
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await ResolveWriteTenantIdAsync(request.TenantId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var policy = new AbacPolicy
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CreatedAt = now,
            UpdatedAt = now
        };

        ApplyRequest(policy, request);
        dbContext.AbacPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePolicyCacheAsync(policy.TenantId, cancellationToken);
        return ToDto(policy);
    }

    public async Task<AbacPolicyDto?> UpdateAsync(
        Guid id,
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = await FindWritableAsync(id, cancellationToken);
        if (policy is null)
        {
            return null;
        }

        var previousTenantId = policy.TenantId;
        policy.TenantId = await ResolveWriteTenantIdAsync(request.TenantId, cancellationToken);
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        ApplyRequest(policy, request);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePolicyCacheAsync(previousTenantId, cancellationToken);
        if (previousTenantId != policy.TenantId)
        {
            await InvalidatePolicyCacheAsync(policy.TenantId, cancellationToken);
        }

        return ToDto(policy);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var policy = await FindWritableAsync(id, cancellationToken);
        if (policy is null)
        {
            return false;
        }

        var tenantId = policy.TenantId;
        dbContext.AbacPolicies.Remove(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        await InvalidatePolicyCacheAsync(tenantId, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<AbacPolicySnapshot>> GetPoliciesAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        return await platformCache.GetOrCreateAsync<IReadOnlyList<AbacPolicySnapshot>>(
            "authorization-policies",
            "active-policies",
            tenantId,
            ["authorization-policies"],
            async token => await dbContext.AbacPolicies
                .AsNoTracking()
                .Where(policy => policy.IsEnabled &&
                    (policy.TenantId == null || policy.TenantId == tenantId))
                .OrderByDescending(policy => policy.Priority)
                .ThenBy(policy => policy.Id)
                .Select(policy => new AbacPolicySnapshot(
                    policy.Id,
                    policy.TenantId,
                    policy.SubjectType,
                    policy.SubjectId,
                    policy.Resource,
                    policy.Action,
                    policy.Effect,
                    policy.ConditionsJson,
                    policy.Priority,
                    policy.IsEnabled))
                .ToArrayAsync(token),
            cancellationToken: cancellationToken) ?? [];
    }

    private async Task<AbacPolicy?> FindWritableAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = dbContext.AbacPolicies.Where(policy => policy.Id == id);
        if (currentTenant.TenantId is Guid tenantId)
        {
            query = query.Where(policy => policy.TenantId == tenantId);
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> ResolveWriteTenantIdAsync(
        Guid? requestedTenantId,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is Guid tenantId)
        {
            return tenantId;
        }

        if (requestedTenantId.HasValue &&
            !await dbContext.Tenants.AnyAsync(
                tenant => tenant.Id == requestedTenantId.Value,
                cancellationToken))
        {
            throw new InvalidOperationException("指定的租户不存在。");
        }

        return requestedTenantId;
    }

    private static void ApplyRequest(AbacPolicy policy, SaveAbacPolicyRequest request)
    {
        policy.Name = request.Name.Trim();
        policy.SubjectType = request.SubjectType.Trim();
        policy.SubjectId = NormalizeOptional(request.SubjectId);
        policy.Resource = request.Resource.Trim();
        policy.Action = request.Action.Trim();
        policy.Effect = request.Effect.Trim();
        policy.ConditionsJson = request.ConditionsJson?.Trim() ?? string.Empty;
        policy.Priority = request.Priority;
        policy.IsEnabled = request.IsEnabled;
        policy.Description = NormalizeOptional(request.Description);
    }

    private static AbacPolicyDto ToDto(AbacPolicy policy)
    {
        return new AbacPolicyDto(
            policy.Id,
            policy.TenantId,
            policy.Name,
            policy.SubjectType,
            policy.SubjectId,
            policy.Resource,
            policy.Action,
            policy.Effect,
            policy.ConditionsJson,
            policy.Priority,
            policy.IsEnabled,
            policy.Description,
            policy.CreatedAt,
            policy.UpdatedAt);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private Task InvalidatePolicyCacheAsync(
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        return platformCache.InvalidateTagsAsync(
            tenantId,
            ["authorization-policies"],
            cancellationToken);
    }
}
