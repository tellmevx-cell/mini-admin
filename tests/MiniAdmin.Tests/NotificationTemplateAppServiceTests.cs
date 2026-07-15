using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.Alerts;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.UserNotifications;
using MiniAdmin.Application.UserNotifications;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Notifications;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class NotificationTemplateAppServiceTests
{
    [Fact]
    public async Task Preview_Renders_Template_With_Variables()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateTemplateService(dbContext);

        var preview = await service.PreviewAsync(new PreviewNotificationTemplateRequest(
            "审批催办：{instanceTitle}",
            "{operatorUserName} 正在催办 {nodeName}",
            "/workflow/center?biz={businessKey}",
            new Dictionary<string, string>
            {
                ["instanceTitle"] = "请假申请",
                ["operatorUserName"] = "admin",
                ["nodeName"] = "直属主管审批",
                ["businessKey"] = "LEAVE-001"
            }));

        Assert.Equal("审批催办：请假申请", preview.Title);
        Assert.Equal("admin 正在催办 直属主管审批", preview.Message);
        Assert.Equal("/workflow/center?biz=LEAVE-001", preview.Link);
    }

    [Fact]
    public async Task Preview_Supports_Scriban_Conditions()
    {
        await using var dbContext = CreateDbContext();
        var service = CreateTemplateService(dbContext);

        var preview = await service.PreviewAsync(new PreviewNotificationTemplateRequest(
            "{{ if level == 'Critical' }}严重告警{{ else }}普通告警{{ end }}",
            "{{ title }}：{{ content }}",
            null,
            new Dictionary<string, string>
            {
                ["level"] = "Critical",
                ["title"] = "磁盘空间",
                ["content"] = "低于 10%"
            }));

        Assert.Equal("严重告警", preview.Title);
        Assert.Equal("磁盘空间：低于 10%", preview.Message);
    }

    [Fact]
    public async Task Alert_Notifications_Use_Enabled_Template()
    {
        await using var dbContext = CreateDbContext();
        var userId = Guid.Parse("f3000000-0000-0000-0000-000000000001");
        var roleId = Guid.Parse("f3000000-0000-0000-0000-000000000002");
        dbContext.Users.Add(new User
        {
            Id = userId,
            UserName = "admin",
            RealName = "Admin",
            PasswordHash = "hash"
        });
        dbContext.Roles.Add(new Role
        {
            Id = roleId,
            Code = "admin",
            Name = "Administrator"
        });
        dbContext.UserRoles.Add(new UserRole
        {
            UserId = userId,
            RoleId = roleId
        });
        dbContext.NotificationTemplates.Add(new NotificationTemplate
        {
            Id = Guid.Parse("f3000000-0000-0000-0000-000000000003"),
            Code = "Alert.Warning",
            Name = "告警警告模板",
            Category = "SystemAlert",
            Level = "Warning",
            Channel = "InApp",
            TitleTemplate = "模板警告：{title}",
            MessageTemplate = "{levelText} - {content}",
            LinkTemplate = "/system/alert",
            IsEnabled = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = CreateUserNotificationService(dbContext);
        var now = DateTimeOffset.UtcNow;
        var created = await service.CreateAlertNotificationsAsync([
            new AlertDto(
                "alert-1",
                "System",
                "Warning",
                "磁盘异常",
                "磁盘空间低于 10%",
                "SystemMonitor",
                "Active",
                now,
                now,
                null,
                null,
                null,
                null,
                1)
        ]);

        Assert.Equal(1, created);
        var notification = Assert.Single(dbContext.UserNotifications);
        Assert.Equal("模板警告：磁盘异常", notification.Title);
        Assert.Equal("警告 - 磁盘空间低于 10%", notification.Message);
        Assert.Equal("/system/alert", notification.Link);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new MiniAdminDbContext(options);
    }

    private static NotificationTemplateAppService CreateTemplateService(MiniAdminDbContext dbContext)
    {
        var repository = new EfNotificationTemplateRepository(
            dbContext,
            new TestCurrentTenant(),
            new TestPlatformCache());
        return new NotificationTemplateAppService(
            repository,
            new ScribanNotificationTemplateRenderer(repository));
    }

    private static UserNotificationAppService CreateUserNotificationService(MiniAdminDbContext dbContext)
    {
        var templateRepository = new EfNotificationTemplateRepository(
            dbContext,
            new TestCurrentTenant(),
            new TestPlatformCache());
        return new UserNotificationAppService(
            new EfUserNotificationRepository(dbContext, new NullRealtimeNotificationPublisher()),
            new ScribanNotificationTemplateRenderer(templateRepository));
    }

    private sealed class TestCurrentTenant : ICurrentTenant
    {
        public Guid? TenantId => null;

        public string? TenantCode => null;

        public bool IsPlatform => true;

        public bool IsTenant => false;
    }
}
