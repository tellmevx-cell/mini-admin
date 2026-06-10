namespace MiniAdmin.Application.Contracts.Tenants;

public sealed record TenantDto(
    string Id,
    string Code,
    string Name,
    string Status,
    string? PackageId,
    string? PackageName,
    string InitializationTemplateCode,
    string InitializationStatus,
    DateTimeOffset? InitializedAt,
    string? InitializationError,
    string? ContactName,
    string? ContactPhone,
    string? ContactEmail,
    DateTimeOffset? ExpireAt,
    string? Remark,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
