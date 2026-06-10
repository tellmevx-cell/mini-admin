using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Application.UserNotifications;

public sealed class UserNotificationAppService(
    IUserNotificationRepository userNotificationRepository,
    INotificationTemplateRenderer notificationTemplateRenderer) : IUserNotificationAppService
{
    public Task<UserNotificationListResult> GetListAsync(
        Guid userId,
        UserNotificationListQuery query,
        CancellationToken cancellationToken = default)
    {
        return userNotificationRepository.GetListAsync(userId, query, cancellationToken);
    }

    public async Task<int> CreateAlertNotificationsAsync(
        IReadOnlyList<AlertDto> alerts,
        CancellationToken cancellationToken = default)
    {
        var requests = await CreateAlertNotificationRequestsAsync(alerts, cancellationToken);

        return requests.Length == 0
            ? 0
            : await userNotificationRepository.CreateForRoleAsync(
                "admin",
                requests,
                DateTimeOffset.UtcNow,
                cancellationToken);
    }

    public async Task<int> CreateAlertNotificationsAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<AlertDto> alerts,
        CancellationToken cancellationToken = default)
    {
        var requests = await CreateAlertNotificationRequestsAsync(alerts, cancellationToken);

        return requests.Length == 0 || userIds.Count == 0
            ? 0
            : await userNotificationRepository.CreateForUsersAsync(
                userIds,
                requests,
                DateTimeOffset.UtcNow,
                cancellationToken);
    }

    private async Task<CreateUserNotificationRequest[]> CreateAlertNotificationRequestsAsync(
        IReadOnlyList<AlertDto> alerts,
        CancellationToken cancellationToken)
    {
        var filteredAlerts = alerts
            .Where(alert => alert.Level is "Warning" or "Critical")
            .ToArray();

        var requests = new List<CreateUserNotificationRequest>(filteredAlerts.Length);
        foreach (var alert in filteredAlerts)
        {
            var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["id"] = alert.Id,
                ["type"] = alert.Type,
                ["level"] = alert.Level,
                ["levelText"] = ToLevelText(alert.Level),
                ["title"] = alert.Title,
                ["content"] = alert.Content,
                ["source"] = alert.Source,
                ["status"] = alert.Status
            };
            var rendered = await notificationTemplateRenderer.RenderAsync(
                $"Alert.{alert.Level}",
                $"[{ToLevelText(alert.Level)}] {alert.Title}",
                alert.Content,
                "/system/alert",
                variables,
                cancellationToken);

            requests.Add(new CreateUserNotificationRequest(
                rendered.Title,
                rendered.Message,
                "SystemAlert",
                alert.Level,
                rendered.Link ?? "/system/alert",
                "Alert",
                alert.Id));
        }

        return requests.ToArray();
    }

    public Task<bool> MarkReadAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return userNotificationRepository.MarkReadAsync(userId, id, cancellationToken);
    }

    public Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return userNotificationRepository.MarkAllReadAsync(userId, cancellationToken);
    }

    public Task<bool> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return userNotificationRepository.DeleteAsync(userId, id, cancellationToken);
    }

    public Task<int> DeleteAllAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return userNotificationRepository.DeleteAllAsync(userId, cancellationToken);
    }

    private static string ToLevelText(string level)
    {
        return level switch
        {
            "Critical" => "严重",
            "Warning" => "警告",
            _ => "提示"
        };
    }
}
