using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Net.Mail;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Notifications;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class NotificationDeliveryServiceTests
{
    [Fact]
    public async Task Creates_Workflow_Email_Delivery_And_Deduplicates_By_Source()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("f1000000-0000-0000-0000-000000000101");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "approver",
            RealName = "Approver",
            PasswordHash = "hash",
            Email = "approver@example.com"
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var firstCreated = await service.CreateWorkflowEmailDeliveryAsync(
            approverId,
            "WorkflowTask",
            "task-001",
            "新的审批待办",
            "你有一条新的审批待办，请及时处理。",
            DateTimeOffset.UtcNow);
        var duplicateCreated = await service.CreateWorkflowEmailDeliveryAsync(
            approverId,
            "WorkflowTask",
            "task-001",
            "新的审批待办",
            "你有一条新的审批待办，请及时处理。",
            DateTimeOffset.UtcNow);

        var delivery = Assert.Single(dbContext.NotificationDeliveries);
        Assert.Equal(1, firstCreated);
        Assert.Equal(0, duplicateCreated);
        Assert.Equal("Email", delivery.Channel);
        Assert.Equal("WorkflowTask", delivery.SourceType);
        Assert.Equal("task-001", delivery.SourceId);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("approver@example.com", delivery.RecipientAddress);
    }

    [Fact]
    public async Task Creates_Workflow_Webhook_Delivery_And_Deduplicates_By_Source()
    {
        await using var dbContext = CreateDbContext();
        var approverId = Guid.Parse("f1000000-0000-0000-0000-000000000102");
        dbContext.Users.Add(new User
        {
            Id = approverId,
            UserName = "webhook-approver",
            RealName = "Webhook Approver",
            PasswordHash = "hash"
        });
        await dbContext.SaveChangesAsync();

        var webhookSender = new StubWebhookNotificationSender();
        var service = CreateService(
            dbContext,
            webhookSender: webhookSender,
            webhookEnabled: true,
            webhookEndpointUrl: "https://hooks.example.com/mini-admin",
            webhookSecret: "secret-token");

        var firstCreated = await service.CreateWorkflowWebhookDeliveryAsync(
            approverId,
            "WorkflowTask",
            "task-webhook-001",
            "新的审批待办",
            "你有一条新的审批待办，请及时处理。",
            DateTimeOffset.UtcNow);
        var duplicateCreated = await service.CreateWorkflowWebhookDeliveryAsync(
            approverId,
            "WorkflowTask",
            "task-webhook-001",
            "新的审批待办",
            "你有一条新的审批待办，请及时处理。",
            DateTimeOffset.UtcNow);

        var delivery = Assert.Single(dbContext.NotificationDeliveries);
        var request = Assert.Single(webhookSender.Requests);
        Assert.Equal(1, firstCreated);
        Assert.Equal(0, duplicateCreated);
        Assert.Equal("Webhook", delivery.Channel);
        Assert.Equal("WorkflowTask", delivery.SourceType);
        Assert.Equal("task-webhook-001", delivery.SourceId);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("https://hooks.example.com/mini-admin", delivery.RecipientAddress);
        Assert.Equal("https://hooks.example.com/mini-admin", request.EndpointUrl);
        Assert.Equal("secret-token", request.Secret);
        Assert.Contains("WorkflowTask", request.PayloadJson);
        Assert.Contains("task-webhook-001", request.PayloadJson);
        Assert.Contains("新的审批待办", request.PayloadJson);
    }

    [Fact]
    public async Task Lists_Deliveries_And_Builds_Channel_Overview()
    {
        await using var dbContext = CreateDbContext();
        var adminId = Guid.Parse("f1000000-0000-0000-0000-000000000001");
        dbContext.Users.Add(new User
        {
            Id = adminId,
            UserName = "admin",
            RealName = "Admin",
            PasswordHash = "hash",
            Email = "admin@example.com"
        });
        dbContext.NotificationDeliveries.AddRange(
            new NotificationDelivery
            {
                Id = Guid.Parse("f2000000-0000-0000-0000-000000000001"),
                Channel = "Email",
                UserId = adminId,
                RecipientAddress = "admin@example.com",
                Title = "告警邮件 1",
                Content = "邮件内容 1",
                SourceType = "Alert",
                SourceId = "alert-1",
                Status = "Succeeded",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
                SentAt = DateTimeOffset.UtcNow.AddMinutes(-9)
            },
            new NotificationDelivery
            {
                Id = Guid.Parse("f2000000-0000-0000-0000-000000000002"),
                Channel = "Email",
                UserId = adminId,
                RecipientAddress = "admin@example.com",
                Title = "告警邮件 2",
                Content = "邮件内容 2",
                SourceType = "Workflow",
                SourceId = "workflow-1",
                Status = "Failed",
                ErrorMessage = "smtp failed",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
            });
        dbContext.UserNotifications.AddRange(
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminId,
                Title = "待处理审批",
                Message = "你有一条新的审批待办",
                Category = "Workflow",
                Level = "Info",
                Link = "/workflow/center",
                SourceType = "WorkflowTask",
                SourceId = "task-1",
                IsRead = false,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-3)
            },
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminId,
                Title = "系统告警",
                Message = "磁盘空间不足",
                Category = "SystemAlert",
                Level = "Warning",
                Link = "/system/alert",
                SourceType = "Alert",
                SourceId = "alert-2",
                IsRead = true,
                ReadAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-2)
            });
        await dbContext.SaveChangesAsync();

        var service = CreateService(dbContext);

        var deliveries = await service.GetListAsync(new NotificationDeliveryListQuery(
            Page: 1,
            PageSize: 10,
            Channel: "Email",
            Status: null,
            SourceType: null));
        var overview = await service.GetChannelOverviewAsync(adminId);

        Assert.Equal(2, deliveries.Total);
        Assert.Equal(2, deliveries.Items.Count);
        Assert.Contains(deliveries.Items, item => item.Status == "Succeeded");
        Assert.Contains(deliveries.Items, item => item.Status == "Failed");

        Assert.Equal(2, overview.TotalNotificationCount);
        Assert.Equal(1, overview.UnreadNotificationCount);
        var emailChannel = Assert.Single(overview.Channels, item => item.Channel == "Email");
        Assert.True(emailChannel.IsEnabled);
        Assert.Equal(1, emailChannel.SucceededCount);
        Assert.Equal(1, emailChannel.FailedCount);
        var webhookChannel = Assert.Single(overview.Channels, item => item.Channel == "Webhook");
        Assert.False(webhookChannel.IsEnabled);
    }

    [Fact]
    public async Task Retry_Failed_Email_Delivery_Sends_Again_And_Updates_Status()
    {
        await using var dbContext = CreateDbContext();
        var adminId = Guid.Parse("f1000000-0000-0000-0000-000000000201");
        var deliveryId = Guid.Parse("f2000000-0000-0000-0000-000000000201");
        dbContext.Users.Add(new User
        {
            Id = adminId,
            UserName = "admin",
            RealName = "Admin",
            PasswordHash = "hash",
            Email = "admin@example.com"
        });
        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            Id = deliveryId,
            Channel = "Email",
            UserId = adminId,
            RecipientAddress = "admin@example.com",
            Title = "失败邮件",
            Content = "邮件内容",
            SourceType = "Alert",
            SourceId = "alert-retry-1",
            Status = "Failed",
            ErrorMessage = "smtp failed",
            RetryCount = 1,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await dbContext.SaveChangesAsync();

        var emailSender = new StubEmailNotificationSender();
        var service = CreateService(dbContext, emailSender: emailSender);

        var result = await service.RetryAsync(deliveryId);

        var request = Assert.Single(emailSender.Requests);
        var delivery = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == deliveryId);
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(2, result.RetryCount);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("admin@example.com", request.To);
        Assert.Equal("失败邮件", request.Subject);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal(2, delivery.RetryCount);
        Assert.Null(delivery.ErrorMessage);
        Assert.NotNull(delivery.SentAt);
    }

    [Fact]
    public async Task Retry_Skipped_Webhook_Delivery_Uses_Current_Endpoint_And_Updates_Status()
    {
        await using var dbContext = CreateDbContext();
        var adminId = Guid.Parse("f1000000-0000-0000-0000-000000000202");
        var deliveryId = Guid.Parse("f2000000-0000-0000-0000-000000000202");
        dbContext.Users.Add(new User
        {
            Id = adminId,
            UserName = "admin",
            RealName = "Admin",
            PasswordHash = "hash"
        });
        dbContext.NotificationDeliveries.Add(new NotificationDelivery
        {
            Id = deliveryId,
            Channel = "Webhook",
            UserId = adminId,
            RecipientAddress = "",
            Title = "失败 Webhook",
            Content = "Webhook 内容",
            SourceType = "WorkflowTask",
            SourceId = "task-retry-1",
            Status = "Skipped",
            ErrorMessage = "Webhook endpoint is not configured.",
            RetryCount = 0,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
        });
        await dbContext.SaveChangesAsync();

        var webhookSender = new StubWebhookNotificationSender();
        var service = CreateService(
            dbContext,
            webhookSender: webhookSender,
            webhookEnabled: true,
            webhookEndpointUrl: "https://hooks.example.com/retry",
            webhookSecret: "retry-secret");

        var result = await service.RetryAsync(deliveryId);

        var request = Assert.Single(webhookSender.Requests);
        var delivery = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == deliveryId);
        Assert.NotNull(result);
        Assert.Equal("Succeeded", result.Status);
        Assert.Equal(1, result.RetryCount);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("https://hooks.example.com/retry", result.RecipientAddress);
        Assert.Equal("https://hooks.example.com/retry", request.EndpointUrl);
        Assert.Equal("retry-secret", request.Secret);
        Assert.Contains("WorkflowTask", request.PayloadJson);
        Assert.Equal("Succeeded", delivery.Status);
        Assert.Equal("https://hooks.example.com/retry", delivery.RecipientAddress);
        Assert.NotNull(delivery.SentAt);
    }

    [Fact]
    public async Task Retry_Failed_Deliveries_Retries_Eligible_Records_Only()
    {
        await using var dbContext = CreateDbContext();
        var adminId = Guid.Parse("f1000000-0000-0000-0000-000000000301");
        var eligibleDeliveryId = Guid.Parse("f2000000-0000-0000-0000-000000000301");
        var maxedDeliveryId = Guid.Parse("f2000000-0000-0000-0000-000000000302");
        dbContext.Users.Add(new User
        {
            Id = adminId,
            UserName = "admin",
            RealName = "Admin",
            PasswordHash = "hash",
            Email = "admin@example.com"
        });
        dbContext.NotificationDeliveries.AddRange(
            new NotificationDelivery
            {
                Id = eligibleDeliveryId,
                Channel = "Email",
                UserId = adminId,
                RecipientAddress = "admin@example.com",
                Title = "可自动重试邮件",
                Content = "邮件内容",
                SourceType = "Alert",
                SourceId = "alert-auto-retry-1",
                Status = "Failed",
                ErrorMessage = "smtp failed",
                RetryCount = 1,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            },
            new NotificationDelivery
            {
                Id = maxedDeliveryId,
                Channel = "Email",
                UserId = adminId,
                RecipientAddress = "admin@example.com",
                Title = "达到上限邮件",
                Content = "邮件内容",
                SourceType = "Alert",
                SourceId = "alert-auto-retry-2",
                Status = "Failed",
                ErrorMessage = "smtp failed",
                RetryCount = 3,
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-9)
            });
        await dbContext.SaveChangesAsync();

        var emailSender = new StubEmailNotificationSender();
        var service = CreateService(dbContext, emailSender: emailSender);

        var result = await service.RetryFailedAsync(maxRetryCount: 3, batchSize: 10);

        var request = Assert.Single(emailSender.Requests);
        var eligible = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == eligibleDeliveryId);
        var maxed = await dbContext.NotificationDeliveries.SingleAsync(x => x.Id == maxedDeliveryId);
        Assert.Single(result.Items);
        Assert.Equal(1, result.RetriedCount);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Equal("admin@example.com", request.To);
        Assert.Equal("Succeeded", eligible.Status);
        Assert.Equal(2, eligible.RetryCount);
        Assert.Equal("Failed", maxed.Status);
        Assert.Equal(3, maxed.RetryCount);
    }

    [Fact]
    public async Task Failed_Workflow_Email_Delivery_Creates_Admin_InApp_Alert()
    {
        await using var dbContext = CreateDbContext();
        var adminId = Guid.Parse("f1000000-0000-0000-0000-000000000401");
        var targetUserId = Guid.Parse("f1000000-0000-0000-0000-000000000402");
        var adminRoleId = Guid.Parse("f1000000-0000-0000-0000-000000000403");
        dbContext.Users.AddRange(
            new User
            {
                Id = adminId,
                UserName = "admin",
                RealName = "Admin",
                PasswordHash = "hash",
                Email = "admin@example.com"
            },
            new User
            {
                Id = targetUserId,
                UserName = "approver",
                RealName = "Approver",
                PasswordHash = "hash",
                Email = "approver@example.com"
            });
        dbContext.Roles.Add(new Role
        {
            Id = adminRoleId,
            Code = "admin",
            Name = "Administrator"
        });
        dbContext.UserRoles.Add(new UserRole
        {
            UserId = adminId,
            RoleId = adminRoleId
        });
        await dbContext.SaveChangesAsync();

        var service = CreateService(
            dbContext,
            emailSender: new FailingEmailNotificationSender("smtp down"));

        var created = await service.CreateWorkflowEmailDeliveryAsync(
            targetUserId,
            "WorkflowTask",
            "task-fail-1",
            "新的审批待办",
            "你有一条新的审批待办，请及时处理。",
            DateTimeOffset.UtcNow);

        var delivery = Assert.Single(dbContext.NotificationDeliveries);
        var alert = Assert.Single(dbContext.UserNotifications);
        Assert.Equal(1, created);
        Assert.Equal("Failed", delivery.Status);
        Assert.Equal("smtp down", delivery.ErrorMessage);
        Assert.Equal(adminId, alert.UserId);
        Assert.Equal("SystemAlert", alert.Category);
        Assert.Equal("Warning", alert.Level);
        Assert.Equal("NotificationDeliveryFailure", alert.SourceType);
        Assert.Equal(delivery.Id.ToString(), alert.SourceId);
        Assert.Contains("邮件", alert.Title);
        Assert.Contains("smtp down", alert.Message);
        Assert.Equal("/system/notification?tab=deliveries&deliveryStatus=Failed", alert.Link);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MiniAdminDbContext(options);
    }

    private static NotificationDeliveryService CreateService(
        MiniAdminDbContext dbContext,
        IEmailNotificationSender? emailSender = null,
        StubWebhookNotificationSender? webhookSender = null,
        bool webhookEnabled = false,
        string webhookEndpointUrl = "",
        string webhookSecret = "")
    {
        return new NotificationDeliveryService(
            dbContext,
            emailSender ?? new StubEmailNotificationSender(),
            Options.Create(new EmailNotificationOptions
            {
                Enabled = true,
                Host = "smtp.example.com",
                FromEmail = "notice@example.com"
            }),
            webhookSender ?? new StubWebhookNotificationSender(),
            Options.Create(new WebhookNotificationOptions
            {
                Enabled = webhookEnabled,
                EndpointUrl = webhookEndpointUrl,
                Secret = webhookSecret
            }),
            new TestCurrentTenant());
    }

    private sealed class StubEmailNotificationSender : IEmailNotificationSender
    {
        public List<EmailRequest> Requests { get; } = [];

        public Task SendAsync(
            string to,
            string subject,
            string content,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new EmailRequest(to, subject, content));
            return Task.CompletedTask;
        }
    }

    private sealed record EmailRequest(
        string To,
        string Subject,
        string Content);

    private sealed class FailingEmailNotificationSender(string errorMessage) : IEmailNotificationSender
    {
        public Task SendAsync(
            string to,
            string subject,
            string content,
            CancellationToken cancellationToken = default)
        {
            throw new SmtpException(errorMessage);
        }
    }

    private sealed class StubWebhookNotificationSender : IWebhookNotificationSender
    {
        public List<WebhookRequest> Requests { get; } = [];

        public Task SendAsync(
            string endpointUrl,
            string payloadJson,
            string? secret,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new WebhookRequest(endpointUrl, payloadJson, secret));
            return Task.CompletedTask;
        }
    }

    private sealed record WebhookRequest(
        string EndpointUrl,
        string PayloadJson,
        string? Secret);

    private sealed class TestCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;

        public string? TenantCode => null;

        public bool IsPlatform => true;

        public bool IsTenant => false;
    }
}
