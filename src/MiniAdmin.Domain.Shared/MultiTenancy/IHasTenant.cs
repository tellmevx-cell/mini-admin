namespace MiniAdmin.Domain.Shared.MultiTenancy;

public interface IHasTenant
{
    Guid? TenantId { get; set; }
}
