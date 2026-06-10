using System.Text.RegularExpressions;
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

public sealed class NotificationTemplateRenderer(
    INotificationTemplateRepository notificationTemplateRepository) : INotificationTemplateRenderer
{
    private static readonly Regex PlaceholderRegex = new(
        "\\{(?<name>[A-Za-z0-9_.-]+)\\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public NotificationTemplatePreviewDto Preview(PreviewNotificationTemplateRequest request)
    {
        var variables = NormalizeVariables(request.Variables);
        return new NotificationTemplatePreviewDto(
            RenderText(request.TitleTemplate, variables),
            RenderText(request.MessageTemplate, variables),
            RenderOptionalText(request.LinkTemplate, variables));
    }

    public async Task<NotificationTemplateRenderResult> RenderAsync(
        string code,
        string fallbackTitle,
        string fallbackMessage,
        string? fallbackLink,
        IReadOnlyDictionary<string, string> variables,
        CancellationToken cancellationToken = default)
    {
        var template = await notificationTemplateRepository.FindByCodeAsync(code, cancellationToken);
        if (template is null || !template.IsEnabled)
        {
            return new NotificationTemplateRenderResult(
                fallbackTitle,
                fallbackMessage,
                fallbackLink,
                null);
        }

        var preview = Preview(new PreviewNotificationTemplateRequest(
            template.TitleTemplate,
            template.MessageTemplate,
            template.LinkTemplate,
            variables));

        return new NotificationTemplateRenderResult(
            preview.Title,
            preview.Message,
            preview.Link,
            template.Code);
    }

    private static string RenderText(
        string template,
        IReadOnlyDictionary<string, string> variables)
    {
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups["name"].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    private static string? RenderOptionalText(
        string? template,
        IReadOnlyDictionary<string, string> variables)
    {
        return string.IsNullOrWhiteSpace(template)
            ? null
            : RenderText(template.Trim(), variables);
    }

    private static IReadOnlyDictionary<string, string> NormalizeVariables(
        IReadOnlyDictionary<string, string>? variables)
    {
        if (variables is null || variables.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var variable in variables)
        {
            normalized[variable.Key] = variable.Value;
        }

        return normalized;
    }
}
