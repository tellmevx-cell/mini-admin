namespace MiniAdmin.Application.Contracts.Tenants;

public sealed record TenantInitializationTemplateDto(
    string Code,
    string Name,
    string Description,
    bool IsDefault);
