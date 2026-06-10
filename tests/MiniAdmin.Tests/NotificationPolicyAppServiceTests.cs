using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class NotificationPolicyAppServiceTests
{
    [Fact]
    public async Task Lists_And_Updates_Notification_Policies()
    {
        await using var dbContext = CreateDbContext();
        var policyId = Guid.Parse("aa000000-0000-0000-0000-000000000001");
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = policyId,
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = true,
            EnableEmail = false,
            EnableWebhook = false,
            IsEnabled = true,
            Remark = "默认策略"
        });
        await dbContext.SaveChangesAsync();

        var service = new NotificationPolicyAppService(new EfNotificationPolicyRepository(dbContext));

        var list = await service.GetListAsync(new NotificationPolicyListQuery(
            Page: 1,
            PageSize: 20,
            Category: "Workflow"));
        var updated = await service.UpdateAsync(
            policyId,
            new SaveNotificationPolicyRequest(
                "审批待办",
                "Workflow",
                "WorkflowDefault",
                false,
                true,
                false,
                true,
                "站内信关闭，邮件预留开启"));

        var item = Assert.Single(list.Items);
        Assert.Equal("WorkflowTask", item.EventCode);
        Assert.NotNull(updated);
        Assert.False(updated.EnableInApp);
        Assert.True(updated.EnableEmail);
        Assert.True(updated.IsEnabled);
        Assert.Equal("站内信关闭，邮件预留开启", updated.Remark);
    }

    [Fact]
    public async Task User_Can_Save_And_Reset_Notification_Subscription()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.Parse("aa000000-0000-0000-0000-000000000201");
        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = "subscriber",
            RealName = "Subscriber",
            PasswordHash = "hash"
        });
        dbContext.NotificationPolicies.Add(new NotificationPolicy
        {
            Id = Guid.Parse("aa000000-0000-0000-0000-000000000202"),
            EventCode = "WorkflowTask",
            EventName = "审批待办",
            Category = "Workflow",
            RecipientStrategy = "WorkflowDefault",
            EnableInApp = true,
            EnableEmail = true,
            EnableWebhook = false,
            IsEnabled = true,
            Remark = "默认策略"
        });
        await dbContext.SaveChangesAsync();

        var service = new NotificationSubscriptionAppService(new EfNotificationSubscriptionRepository(dbContext));

        var saved = await service.SaveMyAsync(
            userId,
            "WorkflowTask",
            new SaveNotificationSubscriptionRequest(
                false,
                true,
                false,
                true));
        var list = await service.GetMyAsync(userId, new NotificationSubscriptionListQuery(Category: "Workflow"));
        var reset = await service.ResetMyAsync(userId, "WorkflowTask");
        var afterReset = await service.GetMyAsync(userId, new NotificationSubscriptionListQuery(Category: "Workflow"));

        var item = Assert.Single(list.Items);
        Assert.Equal("WorkflowTask", saved.EventCode);
        Assert.False(saved.EnableInApp);
        Assert.True(saved.EnableEmail);
        Assert.True(saved.HasCustomPreference);
        Assert.Equal("审批待办", item.EventName);
        Assert.False(item.EnableInApp);
        Assert.True(reset);
        Assert.False(Assert.Single(afterReset.Items).HasCustomPreference);
        Assert.True(Assert.Single(afterReset.Items).EnableInApp);
    }

    [Fact]
    public async Task User_Can_Reset_All_Notification_Subscriptions_To_Policy_Defaults()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.Parse("aa000000-0000-0000-0000-000000000301");
        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = "subscriber",
            RealName = "Subscriber",
            PasswordHash = "hash"
        });
        dbContext.NotificationPolicies.AddRange(
            new NotificationPolicy
            {
                Id = Guid.Parse("aa000000-0000-0000-0000-000000000302"),
                EventCode = "WorkflowTask",
                EventName = "审批待办",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = true,
                EnableWebhook = false,
                IsEnabled = true
            },
            new NotificationPolicy
            {
                Id = Guid.Parse("aa000000-0000-0000-0000-000000000303"),
                EventCode = "WorkflowCc",
                EventName = "流程抄送",
                Category = "Workflow",
                RecipientStrategy = "WorkflowDefault",
                EnableInApp = true,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = true
            });
        dbContext.NotificationSubscriptions.AddRange(
            new NotificationSubscription
            {
                Id = Guid.Parse("aa000000-0000-0000-0000-000000000304"),
                UserId = userId,
                EventCode = "WorkflowTask",
                EnableInApp = false,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            },
            new NotificationSubscription
            {
                Id = Guid.Parse("aa000000-0000-0000-0000-000000000305"),
                UserId = userId,
                EventCode = "WorkflowCc",
                EnableInApp = false,
                EnableEmail = false,
                EnableWebhook = false,
                IsEnabled = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        await dbContext.SaveChangesAsync();

        var service = new NotificationSubscriptionAppService(new EfNotificationSubscriptionRepository(dbContext));

        var resetCount = await service.ResetAllMyAsync(userId);
        var afterReset = await service.GetMyAsync(userId, new NotificationSubscriptionListQuery(Category: "Workflow"));

        Assert.Equal(2, resetCount);
        Assert.All(afterReset.Items, item => Assert.False(item.HasCustomPreference));
        Assert.Contains(afterReset.Items, item =>
            item.EventCode == "WorkflowTask" &&
            item.EnableInApp &&
            item.EnableEmail &&
            item.IsEnabled);
        Assert.Contains(afterReset.Items, item =>
            item.EventCode == "WorkflowCc" &&
            item.EnableInApp &&
            !item.EnableEmail &&
            item.IsEnabled);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MiniAdminDbContext(options);
    }
}
