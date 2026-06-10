using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Application.UserNotifications;

public sealed class NotificationPolicyAppService(
    INotificationPolicyRepository notificationPolicyRepository) : INotificationPolicyAppService
{
    public Task<PageResult<NotificationPolicyDto>> GetListAsync(
        NotificationPolicyListQuery query,
        CancellationToken cancellationToken = default)
    {
        return notificationPolicyRepository.GetListAsync(query, cancellationToken);
    }

    public Task<NotificationPolicyDto?> UpdateAsync(
        Guid id,
        SaveNotificationPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePolicy(request);

        return notificationPolicyRepository.UpdateAsync(
            id,
            request,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    private static void ValidatePolicy(SaveNotificationPolicyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            throw new InvalidOperationException("事件名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("策略分类不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.RecipientStrategy))
        {
            throw new InvalidOperationException("接收人策略不能为空。");
        }
    }
}
