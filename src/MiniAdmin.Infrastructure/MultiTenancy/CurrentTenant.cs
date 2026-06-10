using MiniAdmin.Application.Contracts.MultiTenancy;

namespace MiniAdmin.Infrastructure.MultiTenancy;

public sealed class CurrentTenant : ICurrentTenant
{
    public Guid? TenantId { get; private set; }

    public string? TenantCode { get; private set; }

    public bool IsPlatform => !TenantId.HasValue;

    public bool IsTenant => TenantId.HasValue;

    public void Change(Guid? tenantId, string? tenantCode)
    {
        TenantId = tenantId;
        TenantCode = string.IsNullOrWhiteSpace(tenantCode) ? null : tenantCode;
    }
}
