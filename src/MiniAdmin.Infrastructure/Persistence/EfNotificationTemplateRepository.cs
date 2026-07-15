using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Platform.Caching;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfNotificationTemplateRepository(
    MiniAdminDbContext dbContext,
    ICurrentTenant currentTenant,
    IPlatformCache platformCache) : INotificationTemplateRepository
{
    public async Task<PageResult<NotificationTemplateDto>> GetListAsync(
        NotificationTemplateListQuery query,
        CancellationToken cancellationToken = default)
    {
        var templatesQuery = dbContext.NotificationTemplates.AsNoTracking();
        if (currentTenant.TenantId is Guid tenantId)
        {
            templatesQuery = templatesQuery.Where(template =>
                template.TenantId == null || template.TenantId == tenantId);
        }

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

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        if (currentTenant.TenantId is Guid currentTenantId)
        {
            var effectiveTemplates = (await templatesQuery.ToArrayAsync(cancellationToken))
                .GroupBy(template => template.Code, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(template => template.TenantId == currentTenantId)
                    .First())
                .OrderBy(template => template.Category)
                .ThenBy(template => template.Code)
                .ToArray();
            var effectiveItems = effectiveTemplates
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToDto)
                .ToArray();
            return new PageResult<NotificationTemplateDto>(effectiveItems, effectiveTemplates.Length);
        }

        var total = await templatesQuery.CountAsync(cancellationToken);
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
        return await platformCache.GetOrCreateAsync(
            "notification-templates",
            normalizedCode,
            currentTenant.TenantId,
            [TemplateTag(normalizedCode)],
            async token => await dbContext.NotificationTemplates
                .AsNoTracking()
                .Where(template => template.Code == normalizedCode &&
                    (currentTenant.TenantId.HasValue
                        ? template.TenantId == null || template.TenantId == currentTenant.TenantId
                        : template.TenantId == null))
                .OrderByDescending(template => template.TenantId == currentTenant.TenantId)
                .Select(template => ToDto(template))
                .FirstOrDefaultAsync(token),
            cancellationToken: cancellationToken);
    }

    public async Task<NotificationTemplateDto?> UpdateAsync(
        Guid id,
        SaveNotificationTemplateRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var source = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
            item => item.Id == id &&
                (!currentTenant.TenantId.HasValue ||
                    item.TenantId == null || item.TenantId == currentTenant.TenantId),
            cancellationToken);
        if (source is null)
        {
            return null;
        }

        var template = source;
        if (currentTenant.TenantId is Guid tenantId && source.TenantId is null)
        {
            template = await dbContext.NotificationTemplates.SingleOrDefaultAsync(
                    item => item.TenantId == tenantId && item.Code == source.Code,
                    cancellationToken)
                ?? new NotificationTemplate
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Code = source.Code,
                    CreatedAt = now
                };
            if (dbContext.Entry(template).State == EntityState.Detached)
            {
                dbContext.NotificationTemplates.Add(template);
            }
        }

        ApplyRequest(template, request);
        template.UpdatedAt = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        await platformCache.InvalidateTagsAsync(
            template.TenantId,
            [TemplateTag(template.Code)],
            cancellationToken);
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
            template.UpdatedAt,
            template.TenantId,
            template.TenantId.HasValue);
    }

    private static void ApplyRequest(
        NotificationTemplate template,
        SaveNotificationTemplateRequest request)
    {
        template.Name = request.Name.Trim();
        template.Category = request.Category.Trim();
        template.Level = request.Level.Trim();
        template.Channel = string.IsNullOrWhiteSpace(request.Channel) ? null : request.Channel.Trim();
        template.TitleTemplate = request.TitleTemplate.Trim();
        template.MessageTemplate = request.MessageTemplate.Trim();
        template.LinkTemplate = string.IsNullOrWhiteSpace(request.LinkTemplate) ? null : request.LinkTemplate.Trim();
        template.IsEnabled = request.IsEnabled;
        template.Remark = string.IsNullOrWhiteSpace(request.Remark) ? null : request.Remark.Trim();
    }

    private static string TemplateTag(string code) =>
        $"notification-template:{code.Trim().ToLowerInvariant()}";
}
