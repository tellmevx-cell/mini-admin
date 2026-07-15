using System.Text.RegularExpressions;
using MiniAdmin.Application.Contracts.UserNotifications;
using Scriban;
using Scriban.Runtime;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed partial class ScribanNotificationTemplateRenderer(
    INotificationTemplateRepository notificationTemplateRepository) : INotificationTemplateRenderer
{
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
        string source,
        IReadOnlyDictionary<string, string> variables)
    {
        var legacyResolved = LegacyPlaceholderRegex().Replace(source, match =>
        {
            var key = match.Groups["name"].Value;
            return variables.TryGetValue(key, out var value) ? value : match.Value;
        });
        var template = Template.Parse(legacyResolved);
        if (template.HasErrors)
        {
            throw new InvalidOperationException(
                $"Scriban 模板语法错误：{string.Join("; ", template.Messages)}");
        }

        var globals = new ScriptObject();
        foreach (var variable in variables)
        {
            SetVariable(globals, variable.Key, variable.Value);
        }

        var context = new TemplateContext
        {
            StrictVariables = false
        };
        context.PushGlobal(globals);
        return template.Render(context);
    }

    private static string? RenderOptionalText(
        string? source,
        IReadOnlyDictionary<string, string> variables)
    {
        return string.IsNullOrWhiteSpace(source)
            ? null
            : RenderText(source.Trim(), variables);
    }

    private static IReadOnlyDictionary<string, string> NormalizeVariables(
        IReadOnlyDictionary<string, string>? variables)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (variables is null)
        {
            return normalized;
        }

        foreach (var variable in variables)
        {
            normalized[variable.Key] = variable.Value;
        }

        return normalized;
    }

    private static void SetVariable(ScriptObject root, string name, string value)
    {
        var segments = name.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return;
        }

        var current = root;
        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (!current.TryGetValue(segments[index], out var nested) || nested is not ScriptObject nestedObject)
            {
                nestedObject = new ScriptObject();
                current[segments[index]] = nestedObject;
            }

            current = nestedObject;
        }

        current[segments[^1]] = value;
    }

    [GeneratedRegex(
        "(?<!\\{)\\{(?<name>[A-Za-z0-9_.-]+)\\}(?!\\})",
        RegexOptions.CultureInvariant)]
    private static partial Regex LegacyPlaceholderRegex();
}
