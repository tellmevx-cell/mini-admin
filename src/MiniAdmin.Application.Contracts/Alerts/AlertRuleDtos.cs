using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Alerts;

public sealed record AlertRuleListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Level = null,
    bool? Enabled = null);

public sealed record AlertRuleDto(
    string Id,
    string Code,
    string Name,
    string Description,
    string Metric,
    string Operator,
    decimal Threshold,
    int WindowMinutes,
    string Level,
    bool Enabled,
    bool NotifyEnabled,
    bool EmailEnabled,
    int Sort,
    string? Remark,
    IReadOnlyList<AlertRuleRecipientDto> Recipients,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record AlertRuleRecipientDto(
    string Id,
    string RecipientType,
    string RecipientId,
    string RecipientName);

public sealed record UpdateAlertRuleRequest(
    string Level,
    decimal Threshold,
    int WindowMinutes,
    bool Enabled,
    bool NotifyEnabled,
    bool EmailEnabled,
    IReadOnlyList<Guid> RecipientRoleIds,
    IReadOnlyList<Guid> RecipientUserIds,
    string? Remark);

public interface IAlertRuleRepository
{
    Task<PageResult<AlertRuleDto>> GetListAsync(
        AlertRuleListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRuleDto>> GetEnabledAsync(CancellationToken cancellationToken = default);

    Task<AlertRuleDto?> UpdateAsync(
        Guid id,
        UpdateAlertRuleRequest request,
        CancellationToken cancellationToken = default);
}

public interface IAlertRuleAppService
{
    Task<PageResult<AlertRuleDto>> GetListAsync(
        AlertRuleListQuery query,
        CancellationToken cancellationToken = default);

    Task<AlertRuleDto?> UpdateAsync(
        Guid id,
        UpdateAlertRuleRequest request,
        CancellationToken cancellationToken = default);
}
