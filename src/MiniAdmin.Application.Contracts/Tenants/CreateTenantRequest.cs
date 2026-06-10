namespace MiniAdmin.Application.Contracts.Tenants;

public sealed record CreateTenantRequest(
    string Code,
    string Name,
    Guid? PackageId = null,
    string? InitializationTemplateCode = null,
    string? ContactName = null,
    string? ContactPhone = null,
    string? ContactEmail = null,
    DateTimeOffset? ExpireAt = null,
    string? Remark = null,
    string? AdminUserName = null,
    string? AdminRealName = null,
    string? AdminEmail = null,
    string? AdminPassword = null);
