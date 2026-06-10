using System.Net.Http;
using System.Net.Mail;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.Notifications;

public sealed class NotificationDeliveryService(
    MiniAdminDbContext dbContext,
    IEmailNotificationSender emailNotificationSender,
    IOptions<EmailNotificationOptions> emailOptions,
    IWebhookNotificationSender webhookNotificationSender,
    IOptions<WebhookNotificationOptions> webhookOptions,
    ICurrentTenant currentTenant) : INotificationDeliveryService
{
    public async Task<int> CreateAlertEmailDeliveriesAsync(
        IReadOnlyList<Guid> userIds,
        IReadOnlyList<AlertDto> alerts,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0 || alerts.Count == 0)
        {
            return 0;
        }

        var normalizedUserIds = userIds
            .Where(userId => userId != Guid.Empty)
            .Distinct()
            .ToList();
        var users = await dbContext.Users
            .AsNoTracking()
            .Where(user => normalizedUserIds.Contains(user.Id) && user.IsEnabled)
            .Select(user => new
            {
                user.Id,
                user.Email
            })
            .ToArrayAsync(cancellationToken);

        var created = 0;
        foreach (var user in users)
        {
            foreach (var alert in alerts.Where(alert => alert.Level is "Warning" or "Critical"))
            {
                var exists = await dbContext.NotificationDeliveries.AnyAsync(
                    delivery =>
                        delivery.Channel == "Email" &&
                        delivery.SourceType == "Alert" &&
                        delivery.SourceId == alert.Id &&
                        delivery.UserId == user.Id,
                    cancellationToken);
                if (exists)
                {
                    continue;
                }

                var delivery = new NotificationDelivery
                {
                    Id = Guid.NewGuid(),
                    Channel = "Email",
                    UserId = user.Id,
                    RecipientAddress = NormalizeEmail(user.Email),
                    Title = $"[{ToLevelText(alert.Level)}] {alert.Title}",
                    Content = alert.Content,
                    SourceType = "Alert",
                    SourceId = alert.Id,
                    Status = "Pending",
                    CreatedAt = now
                };

                await UpdateEmailStatusAsync(delivery, cancellationToken);
                await QueueDeliveryFailureAlertAsync(delivery, cancellationToken);
                dbContext.NotificationDeliveries.Add(delivery);
                created++;
            }
        }

        if (created > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return created;
    }

    public async Task<int> CreateWorkflowEmailDeliveryAsync(
        Guid userId,
        string sourceType,
        string sourceId,
        string title,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sourceType) ||
            string.IsNullOrWhiteSpace(sourceId))
        {
            return 0;
        }

        var normalizedSourceType = sourceType.Trim();
        var normalizedSourceId = sourceId.Trim();
        var exists = await dbContext.NotificationDeliveries.AnyAsync(
            delivery =>
                delivery.Channel == "Email" &&
                delivery.SourceType == normalizedSourceType &&
                delivery.SourceId == normalizedSourceId &&
                delivery.UserId == userId,
            cancellationToken);
        if (exists)
        {
            return 0;
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == userId && item.IsEnabled)
            .Select(item => new
            {
                item.Id,
                item.Email
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return 0;
        }

        var delivery = new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            Channel = "Email",
            UserId = user.Id,
            RecipientAddress = NormalizeEmail(user.Email),
            Title = Truncate(title.Trim(), 200),
            Content = Truncate(content.Trim(), 2000),
            SourceType = normalizedSourceType,
            SourceId = normalizedSourceId,
            Status = "Pending",
            CreatedAt = now
        };

        await UpdateEmailStatusAsync(delivery, cancellationToken);
        await QueueDeliveryFailureAlertAsync(delivery, cancellationToken);
        dbContext.NotificationDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);

        return 1;
    }

    public async Task<int> CreateWorkflowWebhookDeliveryAsync(
        Guid userId,
        string sourceType,
        string sourceId,
        string title,
        string content,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty ||
            string.IsNullOrWhiteSpace(sourceType) ||
            string.IsNullOrWhiteSpace(sourceId))
        {
            return 0;
        }

        var normalizedSourceType = sourceType.Trim();
        var normalizedSourceId = sourceId.Trim();
        var exists = await dbContext.NotificationDeliveries.AnyAsync(
            delivery =>
                delivery.Channel == "Webhook" &&
                delivery.SourceType == normalizedSourceType &&
                delivery.SourceId == normalizedSourceId &&
                delivery.UserId == userId,
            cancellationToken);
        if (exists)
        {
            return 0;
        }

        var userExists = await dbContext.Users
            .AsNoTracking()
            .AnyAsync(item => item.Id == userId && item.IsEnabled, cancellationToken);
        if (!userExists)
        {
            return 0;
        }

        var endpointUrl = NormalizeWebhookEndpoint(webhookOptions.Value.EndpointUrl);
        var delivery = new NotificationDelivery
        {
            Id = Guid.NewGuid(),
            Channel = "Webhook",
            UserId = userId,
            RecipientAddress = Truncate(endpointUrl, 256),
            Title = Truncate(title.Trim(), 200),
            Content = Truncate(content.Trim(), 2000),
            SourceType = normalizedSourceType,
            SourceId = normalizedSourceId,
            Status = "Pending",
            CreatedAt = now
        };
        var payloadJson = BuildWorkflowWebhookPayload(
            userId,
            normalizedSourceType,
            normalizedSourceId,
            delivery.Title,
            delivery.Content,
            now);

        await UpdateWebhookStatusAsync(delivery, payloadJson, cancellationToken);
        await QueueDeliveryFailureAlertAsync(delivery, cancellationToken);
        dbContext.NotificationDeliveries.Add(delivery);
        await dbContext.SaveChangesAsync(cancellationToken);

        return 1;
    }

    public async Task<PageResult<NotificationDeliveryDto>> GetListAsync(
        NotificationDeliveryListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var deliveriesQuery = ApplyTenantScope(dbContext.NotificationDeliveries.AsNoTracking())
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            var channel = query.Channel.Trim();
            deliveriesQuery = deliveriesQuery.Where(x => x.Channel == channel);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            deliveriesQuery = deliveriesQuery.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.SourceType))
        {
            var sourceType = query.SourceType.Trim();
            deliveriesQuery = deliveriesQuery.Where(x => x.SourceType == sourceType);
        }

        var total = await deliveriesQuery.CountAsync(cancellationToken);
        var items = await deliveriesQuery
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new NotificationDeliveryDto(
                x.Id.ToString(),
                x.Channel,
                x.UserId.ToString(),
                x.RecipientAddress,
                x.Title,
                x.SourceType,
                x.SourceId,
                x.Status,
                x.ErrorMessage,
                x.RetryCount,
                x.CreatedAt,
                x.SentAt))
            .ToArrayAsync(cancellationToken);

        return new PageResult<NotificationDeliveryDto>(items, total);
    }

    public async Task<NotificationChannelOverviewDto> GetChannelOverviewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var totalNotificationCount = await dbContext.UserNotifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId, cancellationToken);
        var unreadNotificationCount = await dbContext.UserNotifications
            .AsNoTracking()
            .CountAsync(x => x.UserId == userId && !x.IsRead, cancellationToken);

        var emailStats = await ApplyTenantScope(dbContext.NotificationDeliveries.AsNoTracking())
            .Where(x => x.Channel == "Email")
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);

        var emailPendingCount = emailStats
            .Where(x => x.Status == "Pending")
            .Sum(x => x.Count);
        var emailSucceededCount = emailStats
            .Where(x => x.Status == "Succeeded")
            .Sum(x => x.Count);
        var emailFailedCount = emailStats
            .Where(x => x.Status is "Failed" or "Skipped")
            .Sum(x => x.Count);

        var webhookStats = await ApplyTenantScope(dbContext.NotificationDeliveries.AsNoTracking())
            .Where(x => x.Channel == "Webhook")
            .GroupBy(x => x.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count()
            })
            .ToArrayAsync(cancellationToken);

        var webhookPendingCount = webhookStats
            .Where(x => x.Status == "Pending")
            .Sum(x => x.Count);
        var webhookSucceededCount = webhookStats
            .Where(x => x.Status == "Succeeded")
            .Sum(x => x.Count);
        var webhookFailedCount = webhookStats
            .Where(x => x.Status is "Failed" or "Skipped")
            .Sum(x => x.Count);

        var channels = new[]
        {
            new NotificationChannelStatusDto(
                "InApp",
                "站内信",
                true,
                "当前版本已打通工作流与系统告警的站内信提醒。",
                0,
                totalNotificationCount,
                0),
            new NotificationChannelStatusDto(
                "Email",
                "邮件",
                emailOptions.Value.Enabled,
                "邮件投递会记录成功、失败或跳过状态。",
                emailPendingCount,
                emailSucceededCount,
                emailFailedCount),
            new NotificationChannelStatusDto(
                "Webhook",
                "Webhook",
                webhookOptions.Value.Enabled,
                "Webhook 会向配置的外部地址推送 JSON，并记录成功、失败或跳过状态。",
                webhookPendingCount,
                webhookSucceededCount,
                webhookFailedCount)
        };

        return new NotificationChannelOverviewDto(
            totalNotificationCount,
            unreadNotificationCount,
            channels);
    }

    public async Task<NotificationDeliveryDto?> RetryAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var delivery = await ApplyTenantScope(dbContext.NotificationDeliveries)
            .Include(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (delivery is null)
        {
            return null;
        }

        if (delivery.Status == "Succeeded")
        {
            return ToDeliveryDto(delivery);
        }

        delivery.RetryCount++;
        delivery.Status = "Pending";
        delivery.ErrorMessage = null;
        delivery.SentAt = null;

        if (delivery.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
        {
            await RefreshEmailRecipientAsync(delivery, cancellationToken);
            await UpdateEmailStatusAsync(delivery, cancellationToken);
        }
        else if (delivery.Channel.Equals("Webhook", StringComparison.OrdinalIgnoreCase))
        {
            delivery.RecipientAddress = Truncate(
                NormalizeWebhookEndpoint(webhookOptions.Value.EndpointUrl),
                256);
            var payloadJson = BuildWorkflowWebhookPayload(
                delivery.UserId,
                delivery.SourceType,
                delivery.SourceId,
                delivery.Title,
                delivery.Content,
                DateTimeOffset.UtcNow);
            await UpdateWebhookStatusAsync(delivery, payloadJson, cancellationToken);
        }
        else
        {
            delivery.Status = "Skipped";
            delivery.ErrorMessage = $"Channel {delivery.Channel} does not support retry.";
        }

        await QueueDeliveryFailureAlertAsync(delivery, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDeliveryDto(delivery);
    }

    public async Task<NotificationDeliveryRetryBatchResultDto> RetryFailedAsync(
        int maxRetryCount,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var effectiveMaxRetryCount = Math.Max(maxRetryCount, 0);
        if (effectiveMaxRetryCount == 0)
        {
            return new NotificationDeliveryRetryBatchResultDto([], 0, 0, 0, 0);
        }

        var effectiveBatchSize = Math.Clamp(batchSize, 1, 100);
        var deliveryIds = await ApplyTenantScope(dbContext.NotificationDeliveries.AsNoTracking())
            .Where(delivery =>
                (delivery.Status == "Failed" || delivery.Status == "Skipped") &&
                (delivery.Channel == "Email" || delivery.Channel == "Webhook") &&
                delivery.RetryCount < effectiveMaxRetryCount)
            .OrderBy(delivery => delivery.CreatedAt)
            .Select(delivery => delivery.Id)
            .Take(effectiveBatchSize)
            .ToArrayAsync(cancellationToken);

        var retriedDeliveries = new List<NotificationDeliveryDto>();
        foreach (var deliveryId in deliveryIds)
        {
            var retried = await RetryAsync(deliveryId, cancellationToken);
            if (retried is not null)
            {
                retriedDeliveries.Add(retried);
            }
        }

        return new NotificationDeliveryRetryBatchResultDto(
            retriedDeliveries,
            retriedDeliveries.Count,
            retriedDeliveries.Count(delivery => delivery.Status == "Succeeded"),
            retriedDeliveries.Count(delivery => delivery.Status == "Failed"),
            retriedDeliveries.Count(delivery => delivery.Status == "Skipped"));
    }

    private async Task UpdateEmailStatusAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delivery.RecipientAddress))
        {
            delivery.Status = "Skipped";
            delivery.ErrorMessage = "User email is empty.";
            return;
        }

        if (!CanSendEmail(out var reason))
        {
            delivery.Status = "Skipped";
            delivery.ErrorMessage = reason;
            return;
        }

        try
        {
            await emailNotificationSender.SendAsync(
                delivery.RecipientAddress,
                delivery.Title,
                delivery.Content,
                cancellationToken);
            delivery.Status = "Succeeded";
            delivery.ErrorMessage = null;
            delivery.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is SmtpException or InvalidOperationException or FormatException)
        {
            delivery.Status = "Failed";
            delivery.ErrorMessage = Truncate(ex.Message, 1024);
        }
    }

    private async Task UpdateWebhookStatusAsync(
        NotificationDelivery delivery,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(delivery.RecipientAddress))
        {
            delivery.Status = "Skipped";
            delivery.ErrorMessage = "Webhook endpoint is not configured.";
            return;
        }

        if (!CanSendWebhook(out var reason))
        {
            delivery.Status = "Skipped";
            delivery.ErrorMessage = reason;
            return;
        }

        try
        {
            await webhookNotificationSender.SendAsync(
                delivery.RecipientAddress,
                payloadJson,
                NormalizeSecret(webhookOptions.Value.Secret),
                cancellationToken);
            delivery.Status = "Succeeded";
            delivery.ErrorMessage = null;
            delivery.SentAt = DateTimeOffset.UtcNow;
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or FormatException or TaskCanceledException)
        {
            delivery.Status = "Failed";
            delivery.ErrorMessage = Truncate(ex.Message, 1024);
        }
    }

    private bool CanSendEmail(out string? reason)
    {
        var options = emailOptions.Value;
        if (!options.Enabled)
        {
            reason = "Email notification is disabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.Host) || string.IsNullOrWhiteSpace(options.FromEmail))
        {
            reason = "SMTP host or from email is not configured.";
            return false;
        }

        reason = null;
        return true;
    }

    private async Task RefreshEmailRecipientAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        var email = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == delivery.UserId && user.IsEnabled)
            .Select(user => user.Email)
            .SingleOrDefaultAsync(cancellationToken);
        delivery.RecipientAddress = NormalizeEmail(email);
    }

    private bool CanSendWebhook(out string? reason)
    {
        var options = webhookOptions.Value;
        if (!options.Enabled)
        {
            reason = "Webhook notification is disabled.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.EndpointUrl))
        {
            reason = "Webhook endpoint is not configured.";
            return false;
        }

        reason = null;
        return true;
    }

    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email.Trim();
    }

    private static string NormalizeWebhookEndpoint(string? endpointUrl)
    {
        return string.IsNullOrWhiteSpace(endpointUrl) ? string.Empty : endpointUrl.Trim();
    }

    private static string? NormalizeSecret(string? secret)
    {
        return string.IsNullOrWhiteSpace(secret) ? null : secret.Trim();
    }

    private static string BuildWorkflowWebhookPayload(
        Guid userId,
        string sourceType,
        string sourceId,
        string title,
        string content,
        DateTimeOffset createdAt)
    {
        return JsonSerializer.Serialize(
            new
            {
                channel = "Webhook",
                category = "Workflow",
                eventCode = sourceType,
                sourceType,
                sourceId,
                userId,
                title,
                content,
                createdAt
            },
            WebhookJsonOptions);
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

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private async Task QueueDeliveryFailureAlertAsync(
        NotificationDelivery delivery,
        CancellationToken cancellationToken)
    {
        if (!IsDeliveryFailureStatus(delivery.Status))
        {
            return;
        }

        var recipientUserIds = await ResolveDeliveryFailureAlertRecipientIdsAsync(cancellationToken);
        if (recipientUserIds.Count == 0)
        {
            return;
        }

        var sourceId = delivery.Id.ToString();
        foreach (var userId in recipientUserIds)
        {
            if (await HasExistingDeliveryFailureAlertAsync(userId, sourceId, cancellationToken))
            {
                continue;
            }

            var channelText = ToChannelText(delivery.Channel);
            var reason = string.IsNullOrWhiteSpace(delivery.ErrorMessage)
                ? delivery.Status
                : delivery.ErrorMessage;
            dbContext.UserNotifications.Add(new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Title = $"通知投递异常：{channelText}",
                Message = $"{channelText}投递未成功，标题“{delivery.Title}”，原因：{Truncate(reason, 500)}",
                Category = "SystemAlert",
                Level = "Warning",
                Link = CreateDeliveryFailureAlertLink(delivery),
                SourceType = "NotificationDeliveryFailure",
                SourceId = sourceId,
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
    }

    private async Task<IReadOnlyList<Guid>> ResolveDeliveryFailureAlertRecipientIdsAsync(
        CancellationToken cancellationToken)
    {
        var query = dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole =>
                userRole.Role.Code == "admin" &&
                userRole.Role.IsEnabled &&
                userRole.User.IsEnabled);

        if (currentTenant.IsTenant && currentTenant.TenantId.HasValue)
        {
            query = query.Where(userRole =>
                userRole.User.TenantId == currentTenant.TenantId.Value &&
                userRole.Role.TenantId == currentTenant.TenantId.Value);
        }
        else if (currentTenant.IsPlatform)
        {
            query = query.Where(userRole =>
                userRole.User.TenantId == null &&
                userRole.Role.TenantId == null);
        }

        return await query
            .Select(userRole => userRole.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    private async Task<bool> HasExistingDeliveryFailureAlertAsync(
        Guid userId,
        string sourceId,
        CancellationToken cancellationToken)
    {
        const string sourceType = "NotificationDeliveryFailure";
        if (dbContext.UserNotifications.Local.Any(notification =>
                notification.UserId == userId &&
                notification.SourceType == sourceType &&
                notification.SourceId == sourceId))
        {
            return true;
        }

        return await dbContext.UserNotifications
            .AsNoTracking()
            .AnyAsync(notification =>
                notification.UserId == userId &&
                notification.SourceType == sourceType &&
                notification.SourceId == sourceId,
                cancellationToken);
    }

    private static bool IsDeliveryFailureStatus(string status)
    {
        return status is "Failed" or "Skipped";
    }

    private static string ToChannelText(string channel)
    {
        return channel switch
        {
            "Email" => "邮件",
            "Webhook" => "Webhook",
            _ => channel
        };
    }

    private static string CreateDeliveryFailureAlertLink(NotificationDelivery delivery)
    {
        return $"/system/notification?tab=deliveries&deliveryStatus={Uri.EscapeDataString(delivery.Status)}";
    }

    private static NotificationDeliveryDto ToDeliveryDto(NotificationDelivery delivery)
    {
        return new NotificationDeliveryDto(
            delivery.Id.ToString(),
            delivery.Channel,
            delivery.UserId.ToString(),
            delivery.RecipientAddress,
            delivery.Title,
            delivery.SourceType,
            delivery.SourceId,
            delivery.Status,
            delivery.ErrorMessage,
            delivery.RetryCount,
            delivery.CreatedAt,
            delivery.SentAt);
    }

    private static readonly JsonSerializerOptions WebhookJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private IQueryable<NotificationDelivery> ApplyTenantScope(IQueryable<NotificationDelivery> query)
    {
        if (currentTenant.IsTenant && currentTenant.TenantId.HasValue)
        {
            return query.Where(x => x.User.TenantId == currentTenant.TenantId.Value);
        }

        if (currentTenant.IsPlatform)
        {
            return query.Where(x => x.User.TenantId == null);
        }

        return query;
    }
}
