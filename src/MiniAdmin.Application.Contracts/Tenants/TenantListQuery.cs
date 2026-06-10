namespace MiniAdmin.Application.Contracts.Tenants;

public sealed record TenantListQuery(
    int Page = 1,
    int PageSize = 10,
    string? Code = null,
    string? Name = null,
    string? Status = null);
