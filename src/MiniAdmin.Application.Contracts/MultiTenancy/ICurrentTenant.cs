namespace MiniAdmin.Application.Contracts.MultiTenancy;

public interface ICurrentTenant
{
    Guid? TenantId { get; }

    string? TenantCode { get; }

    bool IsPlatform { get; }

    bool IsTenant { get; }
}
