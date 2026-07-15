using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.UserNotifications;

namespace MiniAdmin.Application.UserNotifications;

public sealed class NotificationTemplateAppService(
    INotificationTemplateRepository notificationTemplateRepository,
    INotificationTemplateRenderer notificationTemplateRenderer) : INotificationTemplateAppService
{
    public Task<PageResult<NotificationTemplateDto>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default)
    {
        return notificationTemplateRepository.GetListAsync(query, cancellationToken);
    }

    public Task<NotificationTemplateDto?> UpdateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTemplate(request);

        return notificationTemplateRepository.UpdateAsync(id, request, DateTimeOffset.UtcNow, cancellationToken);
    }

    public Task<NotificationTemplatePreviewDto> PreviewAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePreview(request);

        return Task.FromResult(notificationTemplateRenderer.Preview(request));
    }

    private static void ValidateTemplate(SaveNotificationTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("模板名称不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("模板分类不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.Level))
        {
            throw new InvalidOperationException("模板等级不能为空。");
        }

        ValidatePreview(new PreviewNotificationTemplateRequest(
            request.TitleTemplate,
            request.MessageTemplate,
            request.LinkTemplate,
            null));
    }

    private static void ValidatePreview(PreviewNotificationTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TitleTemplate))
        {
            throw new InvalidOperationException("标题模板不能为空。");
        }

        if (string.IsNullOrWhiteSpace(request.MessageTemplate))
        {
            throw new InvalidOperationException("内容模板不能为空。");
        }
    }
}
