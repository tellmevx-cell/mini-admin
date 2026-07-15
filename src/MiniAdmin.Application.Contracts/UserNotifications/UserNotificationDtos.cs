using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.UserNotifications;

public sealed record UserNotificationListQuery(
    int? Take = null,
    int Page = 1,
    int PageSize = 20,
    bool? IsRead = null,
    string? Category = null,
    string? SourceType = null);

public sealed record UserNotificationDto(
    string Id,
    string Title,
    string Message,
    string Category,
    string Level,
    string? Link,
    string SourceType,
    string SourceId,
    bool IsRead,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt);

public sealed record UserNotificationListResult(
    IReadOnlyList<UserNotificationDto> Items,
    int Total,
    int UnreadCount);

public sealed record NotificationDeliveryListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Channel = null,
    string? Status = null,
    string? SourceType = null);

public sealed record NotificationDeliveryDto(
    string Id,
    string Channel,
    string UserId,
    string RecipientAddress,
    string Title,
    string SourceType,
    string SourceId,
    string Status,
    string? ErrorMessage,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt);

public sealed record NotificationDeliveryRetryBatchResultDto(
    IReadOnlyList<NotificationDeliveryDto> Items,
    int RetriedCount,
    int SucceededCount,
    int FailedCount,
    int SkippedCount);

public sealed record NotificationChannelStatusDto(
    string Channel,
    string DisplayName,
    bool IsEnabled,
    string Description,
    int PendingCount,
    int SucceededCount,
    int FailedCount);

public sealed record NotificationChannelOverviewDto(
    int TotalNotificationCount,
    int UnreadNotificationCount,
    IReadOnlyList<NotificationChannelStatusDto> Channels);

public sealed record NotificationTemplateListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Category = null,
    string? Code = null,
    bool? IsEnabled = null);

public sealed record NotificationTemplateDto(
    string Id,
    string Code,
    string Name,
    string Category,
    string Level,
    string? Channel,
    string TitleTemplate,
    string MessageTemplate,
    string? LinkTemplate,
    bool IsEnabled,
    string? Remark,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid? TenantId = null,
    bool IsTenantOverride = false);

public sealed record SaveNotificationTemplateRequest(
    string Name,
    string Category,
    string Level,
    string? Channel,
    string TitleTemplate,
    string MessageTemplate,
    string? LinkTemplate,
    bool IsEnabled,
    string? Remark);

public sealed record PreviewNotificationTemplateRequest(
    string TitleTemplate,
    string MessageTemplate,
    string? LinkTemplate,
    IReadOnlyDictionary<string, string>? Variables);

public sealed record NotificationTemplatePreviewDto(
    string Title,
    string Message,
    string? Link);

public sealed record NotificationTemplateRenderResult(
    string Title,
    string Message,
    string? Link,
    string? TemplateCode);

public sealed record NotificationPolicyListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Category = null,
    string? EventCode = null,
    bool? IsEnabled = null);

public sealed record NotificationPolicyDto(
    string Id,
    string EventCode,
    string EventName,
    string Category,
    string RecipientStrategy,
    bool EnableInApp,
    bool EnableEmail,
    bool EnableWebhook,
    bool IsEnabled,
    string? Remark,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SaveNotificationPolicyRequest(
    string EventName,
    string Category,
    string RecipientStrategy,
    bool EnableInApp,
    bool EnableEmail,
    bool EnableWebhook,
    bool IsEnabled,
    string? Remark);

public sealed record NotificationSubscriptionListQuery(
    string? Keyword = null,
    string? Category = null);

public sealed record NotificationSubscriptionDto(
    string? Id,
    string EventCode,
    string EventName,
    string Category,
    bool PolicyEnableInApp,
    bool PolicyEnableEmail,
    bool PolicyEnableWebhook,
    bool EnableInApp,
    bool EnableEmail,
    bool EnableWebhook,
    bool IsEnabled,
    bool HasCustomPreference,
    DateTimeOffset? UpdatedAt);

public sealed record NotificationSubscriptionListResult(
    IReadOnlyList<NotificationSubscriptionDto> Items,
    int Total);

public sealed record SaveNotificationSubscriptionRequest(
    bool EnableInApp,
    bool EnableEmail,
    bool EnableWebhook,
    bool IsEnabled);

public sealed record CreateUserNotificationRequest(
    string Title,
    string Message,
    string Category,
    string Level,
    string? Link,
    string SourceType,
    string SourceId);

public interface IUserNotificationRepository
{
    Task<UserNotificationListResult> GetListAsync(
        Guid userId,
        UserNotificationListQuery query,
        CancellationToken cancellationToken = default);

    Task<int> CreateForRoleAsync(
        string roleCode,
        IReadOnlyList<CreateUserNotificationRequest> requests,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> CreateForUsersAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<CreateUserNotificationRequest> requests,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IRealtimeNotificationPublisher
{
    Task PublishCreatedAsync(
        Guid userId,
        IReadOnlyList<UserNotificationDto> notifications,
        int unreadCount,
        CancellationToken cancellationToken = default);

    Task PublishUnreadCountAsync(
        Guid userId,
        int unreadCount,
        CancellationToken cancellationToken = default);
}

public interface INotificationTemplateRepository
{
    Task<PageResult<NotificationTemplateDto>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto?> UpdateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface INotificationPolicyRepository
{
    Task<PageResult<NotificationPolicyDto>> GetListAsync(
        NotificationPolicyListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationPolicyDto?> FindByEventCodeAsync(
        string eventCode,
        CancellationToken cancellationToken = default);

    Task<NotificationPolicyDto?> UpdateAsync(
        Guid id,
        SaveNotificationPolicyRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface INotificationSubscriptionRepository
{
    Task<NotificationSubscriptionListResult> GetMyAsync(
        Guid userId,
        NotificationSubscriptionListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationSubscriptionDto?> SaveMyAsync(
        Guid userId,
        string eventCode,
        SaveNotificationSubscriptionRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> ResetMyAsync(
        Guid userId,
        string eventCode,
        CancellationToken cancellationToken = default);

    Task<int> ResetAllMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface INotificationTemplateRenderer
{
    NotificationTemplatePreviewDto Preview(PreviewNotificationTemplateRequest request);

    Task<NotificationTemplateRenderResult> RenderAsync(
        string code,
        string fallbackTitle,
        string fallbackMessage,
        string? fallbackLink,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default);
}

public interface INotificationTemplateAppService
{
    Task<PageResult<NotificationTemplateDto>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto?> UpdateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplatePreviewDto> PreviewAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);
}

public interface INotificationPolicyAppService
{
    Task<PageResult<NotificationPolicyDto>> GetListAsync(
        NotificationPolicyListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationPolicyDto?> UpdateAsync(
        Guid id,
        SaveNotificationPolicyRequest request,
        CancellationToken cancellationToken = default);
}

public interface INotificationSubscriptionAppService
{
    Task<NotificationSubscriptionListResult> GetMyAsync(
        Guid userId,
        NotificationSubscriptionListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationSubscriptionDto?> SaveMyAsync(
        Guid userId,
        string eventCode,
        SaveNotificationSubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> ResetMyAsync(
        Guid userId,
        string eventCode,
        CancellationToken cancellationToken = default);

    Task<int> ResetAllMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IUserNotificationAppService
{
    Task<UserNotificationListResult> GetListAsync(
        Guid userId,
        UserNotificationListQuery query,
        CancellationToken cancellationToken = default);

    Task<int> CreateAlertNotificationsAsync(
        IReadOnlyList<AlertDto> alerts,
        CancellationToken cancellationToken = default);

    Task<int> CreateAlertNotificationsAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<AlertDto> alerts,
        CancellationToken cancellationToken = default);

    Task<bool> MarkReadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

public interface IAlertNotificationRecipientRepository
{
    Task<IReadOnlyList<Guid>> ResolveUserIdsAsync(
        AlertRuleDto rule,
        CancellationToken cancellationToken = default);
}

public interface INotificationDeliveryService
{
    Task<int> CreateAlertEmailDeliveriesAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<AlertDto> alerts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> CreateWorkflowEmailDeliveryAsync(
        Guid userId,
        string sourceType,
        string sourceId,
        string title,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<int> CreateWorkflowWebhookDeliveryAsync(
        Guid userId,
        string sourceType,
        string sourceId,
        string title,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<PageResult<NotificationDeliveryDto>> GetListAsync(
        NotificationDeliveryListQuery query,
        CancellationToken cancellationToken = default);

    Task<NotificationDeliveryDto?> RetryAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<NotificationDeliveryRetryBatchResultDto> RetryFailedAsync(
        int maxRetryCount,
        int batchSize,
        CancellationToken cancellationToken = default);

    Task<NotificationChannelOverviewDto> GetChannelOverviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
