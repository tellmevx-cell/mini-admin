namespace MiniAdmin.Application.Contracts.Authorization;

public sealed record AbacPolicyDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string SubjectType,
    string? SubjectId,
    string Resource,
    string Action,
    string Effect,
    string ConditionsJson,
    int Priority,
    bool IsEnabled,
    string? Description,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveAbacPolicyRequest(
    Guid? TenantId,
    string Name,
    string SubjectType,
    string? SubjectId,
    string Resource,
    string Action,
    string Effect,
    string? ConditionsJson,
    int Priority,
    bool IsEnabled,
    string? Description);

public interface IAbacPolicyRepository
{
    Task<IReadOnlyList<AbacPolicyDto>> GetListAsync(CancellationToken cancellationToken = default);

    Task<AbacPolicyDto> CreateAsync(
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<AbacPolicyDto?> UpdateAsync(
        Guid id,
        SaveAbacPolicyRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
