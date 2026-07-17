using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.TenantResourceQuotas;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class TenantResourceQuotaWarningTests :
    IClassFixture<WebApplicationFactory<Program>>,
    IDisposable
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> factory;

    public TenantResourceQuotaWarningTests(WebApplicationFactory<Program> rootFactory)
    {
        factory = rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:InMemoryDatabaseName"] = $"MiniAdminQuotaWarningTests-{Guid.NewGuid():N}",
                    ["Cache:Provider"] = "Memory",
                    ["Cache:Redis:Configuration"] = string.Empty,
                    ["RateLimiting:Enabled"] = "false"
                });
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task TenantResourceQuotaWarning_TracksTransitionsAndAvoidsDuplicateNotifications()
    {
        var tenant = await ProvisionTenantAsync(maxUsers: 5);
        await AddTenantUsersAsync(tenant.Id, tenant.Unique, 3);

        var firstScan = await ScanAsync();
        var firstWarning = Assert.Single(firstScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.ResourceType == TenantResourceTypes.Users);
        Assert.Equal(TenantQuotaStatuses.Warning, firstWarning.Status);
        Assert.Equal(1, firstWarning.NotificationCount);

        var duplicateScan = await ScanAsync();
        var duplicateWarning = Assert.Single(duplicateScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.ResourceType == TenantResourceTypes.Users);
        Assert.Equal(0, duplicateWarning.NotificationCount);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var adminId = await dbContext.Users
                .Where(user => user.UserName == tenant.AdminUserName)
                .Select(user => user.Id)
                .SingleAsync();
            var notifications = await dbContext.UserNotifications
                .Where(item => item.UserId == adminId && item.SourceType == "TenantQuota")
                .ToArrayAsync();
            var warningState = await dbContext.TenantResourceQuotaWarnings.SingleAsync(item =>
                item.TenantId == tenant.Id &&
                item.ResourceType == TenantResourceTypes.Users);

            Assert.Single(notifications);
            Assert.Equal(TenantQuotaStatuses.Warning, warningState.Status);
            Assert.Equal(TenantQuotaStatuses.Warning, warningState.LastNotifiedStatus);
            Assert.Equal(1, warningState.NotificationSequence);
        }

        await RemoveTenantUsersAsync(tenant.Id, 2);
        var recoveryScan = await ScanAsync();
        Assert.DoesNotContain(recoveryScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.ResourceType == TenantResourceTypes.Users);

        await AddTenantUsersAsync(tenant.Id, $"{tenant.Unique}-again", 2);
        var secondWarningScan = await ScanAsync();
        var secondWarning = Assert.Single(secondWarningScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.ResourceType == TenantResourceTypes.Users);
        Assert.Equal(TenantQuotaStatuses.Warning, secondWarning.Status);
        Assert.Equal(1, secondWarning.NotificationCount);

        await AuthorizeAsync(tenant.AdminUserName, tenant.AdminPassword, tenant.Code);
        var usageResponse = await client.GetAsync("/tenant/resource-usage");
        Assert.True(
            usageResponse.IsSuccessStatusCode,
            await usageResponse.Content.ReadAsStringAsync());
        var usage = await usageResponse.Content
            .ReadFromJsonAsync<TenantResourceUsageDto>();
        Assert.NotNull(usage);
        Assert.Equal(4, usage.Users.UsedValue);
        Assert.Equal(5, usage.Users.LimitValue);
        Assert.Equal(80m, usage.Users.UsagePercent);
        Assert.Equal(TenantQuotaStatuses.Warning, usage.Users.Status);
        Assert.Equal(TenantQuotaStatuses.Unlimited, usage.Storage.Status);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var adminId = await dbContext.Users
                .Where(user => user.UserName == tenant.AdminUserName)
                .Select(user => user.Id)
                .SingleAsync();
            var notificationCount = await dbContext.UserNotifications.CountAsync(item =>
                item.UserId == adminId && item.SourceType == "TenantQuota");
            var warningState = await dbContext.TenantResourceQuotaWarnings.SingleAsync(item =>
                item.TenantId == tenant.Id &&
                item.ResourceType == TenantResourceTypes.Users);

            Assert.Equal(2, notificationCount);
            Assert.Equal(2, warningState.NotificationSequence);
            Assert.NotNull(warningState.LastNotifiedAt);
        }
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private async Task<ProvisionedTenant> ProvisionTenantAsync(int maxUsers)
    {
        await AuthorizeAsync("admin", "123456", null);
        var unique = Guid.NewGuid().ToString("N")[..8];
        var packageId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var tenantAdminRoleId = await dbContext.Roles
                .Where(role => role.Code == "tenant-admin")
                .Select(role => role.Id)
                .SingleAsync();
            var menuIds = await dbContext.RoleMenus
                .Where(item => item.RoleId == tenantAdminRoleId)
                .Select(item => item.MenuId)
                .ToArrayAsync();
            dbContext.TenantPackages.Add(new TenantPackage
            {
                Id = packageId,
                Name = $"Quota warning package {unique}",
                MaxUsers = maxUsers,
                MaxStorageMb = 0,
                MenuIds = JsonSerializer.Serialize(menuIds),
                IsEnabled = true
            });
            await dbContext.SaveChangesAsync();
        }

        var code = $"quota-warning-{unique}";
        var adminUserName = $"quota-warning-admin-{unique}";
        var adminPassword = $"Quota{unique}1";
        var response = await client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = $"配额预警测试租户 {unique}",
            packageId,
            initializationTemplateCode = "standard",
            adminUserName,
            adminRealName = "配额预警管理员",
            adminPassword
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var created = await response.Content.ReadFromJsonAsync<ApiEnvelope<TenantCreatedData>>();
        Assert.NotNull(created);

        return new ProvisionedTenant(
            Guid.Parse(created.Data.Id),
            code,
            adminUserName,
            adminPassword,
            unique);
    }

    private async Task AddTenantUsersAsync(Guid tenantId, string suffix, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        for (var index = 0; index < count; index++)
        {
            dbContext.Users.Add(new User
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserName = $"quota-warning-user-{suffix}-{index}",
                RealName = "配额预警测试用户",
                PasswordHash = "not-used",
                SecurityStamp = Guid.NewGuid().ToString("N"),
                IsEnabled = true
            });
        }

        await dbContext.SaveChangesAsync();
    }

    private async Task RemoveTenantUsersAsync(Guid tenantId, int count)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var users = await dbContext.Users
            .Where(user => user.TenantId == tenantId && !user.UserRoles.Any())
            .Take(count)
            .ToArrayAsync();
        dbContext.Users.RemoveRange(users);
        await dbContext.SaveChangesAsync();
    }

    private async Task<TenantResourceQuotaScanResult> ScanAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITenantResourceQuotaWarningService>();
        return await service.ScanAsync();
    }

    private async Task AuthorizeAsync(string username, string password, string? tenantCode)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            username,
            password,
            tenantCode
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var login = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        Assert.NotNull(login);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.Data.AccessToken);
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);
    private sealed record LoginData(string AccessToken);
    private sealed record TenantCreatedData(string Id, string Code);
    private sealed record ProvisionedTenant(
        Guid Id,
        string Code,
        string AdminUserName,
        string AdminPassword,
        string Unique);
}
