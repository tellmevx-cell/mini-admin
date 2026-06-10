using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Domain.Entities;

public sealed class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public TenantStatus Status { get; set; } = TenantStatus.Pending;

    public Guid? PackageId { get; set; }

    public TenantPackage? Package { get; set; }

    public string InitializationTemplateCode { get; set; } = "standard";

    public string InitializationStatus { get; set; } = "Pending";

    public DateTimeOffset? InitializedAt { get; set; }

    public string? InitializationError { get; set; }

    public string? ContactName { get; set; }

    public string? ContactPhone { get; set; }

    public string? ContactEmail { get; set; }

    public DateTimeOffset? ExpireAt { get; set; }

    public string? Remark { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
