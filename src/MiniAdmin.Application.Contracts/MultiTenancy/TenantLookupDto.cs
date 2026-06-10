using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Application.Contracts.MultiTenancy;

public sealed record TenantLookupDto(
    Guid Id,
    string Name,
    string Code,
    TenantStatus Status,
    DateTimeOffset? ExpireAt);
