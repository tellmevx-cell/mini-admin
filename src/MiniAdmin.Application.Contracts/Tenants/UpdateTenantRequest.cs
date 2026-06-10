namespace MiniAdmin.Application.Contracts.Tenants;

public sealed record UpdateTenantRequest(
    string Name,
    Guid? PackageId = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? ContactEmail = null,
    DateTimeOffset? ExpireAt = null,
    string? Remark = null);
