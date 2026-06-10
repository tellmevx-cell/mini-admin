using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfNotificationTemplateRepository(MiniAdminDbContext dbContext) : INotificationTemplateRepository
{
    public async Task<PageResult<NotificationTemplateDto>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default)
    {
        var templatesQuery = dbContext.NotificationTemplates.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            templatesQuery = templatesQuery.Where(template =>
                template.Code.Contains(keyword) ||
                template.Name.Contains(keyword) ||
                template.TitleTemplate.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            templatesQuery = templatesQuery.Where(template => template.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(query.Code))
        {
            var code = query.Code.Trim();
            templatesQuery = templatesQuery.Where(template => template.Code == code);
        }

        if (query.IsEnabled.HasValue)
        {
            templatesQuery = templatesQuery.Where(template => template.IsEnabled == query.IsEnabled.Value);
        }

        var total = await templatesQuery.CountAsync(cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await templatesQuery
            .OrderBy(template => template.Category)
            .ThenBy(template => template.Code)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(template => ToDto(template))
            .ToArrayAsync(cancellationToken);

        return new PageResult<NotificationTemplateDto>(items, total);
    }

    public async Task<NotificationTemplateDto?> FindByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim();
        return await dbContext.NotificationTemplates
            .AsNoTracking()
            .Where(template => template.Code == normalizedCode)
            .Select(template => ToDto(template))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<NotificationTemplateDto?> UpdateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            item => item.Id == id,
            cancellationToken);
        if (template is null)
        {
            return null;
        }

        template.Name = request.Name.Trim();
        template.Category = request.Category.Trim();
        template.Level = request.Level.Trim();
        template.Channel = string.IsNullOrWhiteSpace(request.Channel) ? null : request.Channel.Trim();
        template.TitleTemplate = request.TitleTemplate.Trim();
        template.MessageTemplate = request.MessageTemplate.Trim();
        template.LinkTemplate = string.IsNullOrWhiteSpace(request.LinkTemplate) ? null : request.LinkTemplate.Trim();
        template.IsEnabled = request.IsEnabled;
        template.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
        template.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(template);
    }

    private static NotificationTemplateDto ToDto(NotificationTemplate template)
    {
        return new NotificationTemplateDto(
            template.Id.ToString(),
            template.Code,
            template.Name,
            template.Category,
            template.Level,
            template.Channel,
            template.TitleTemplate,
            template.MessageTemplate,
            template.LinkTemplate,
            template.IsEnabled,
            template.Remark,
            template.CreatedAt,
            template.UpdatedAt);
    }
}
