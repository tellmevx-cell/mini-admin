using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Tenants;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class TenantLifecycleTests :
    IClassFixture<WebApplicationFactory<Program>>,
    IDisposable
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> factory;

    public TenantLifecycleTests(WebApplicationFactory<Program> rootFactory)
    {
        factory = rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:InMemoryDatabaseName"] = $"MiniAdminTenantLifecycleTests-{Guid.NewGuid():N}",
                    ["Cache:Provider"] = "Memory",
                    ["Cache:Redis:Configuration"] = string.Empty,
                    ["RateLimiting:Enabled"] = "false"
                });
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task LifecycleScan_SendsSingleReminderForCurrentThreshold()
    {
        await AuthorizePlatformAsync();
        var tenant = await ProvisionTenantAsync(DateTimeOffset.UtcNow.AddDays(6));

        var firstScan = await ScanAsync();
        Assert.Contains(firstScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.EventType == TenantLifecycleEventTypes.ExpiryReminder &&
            item.ReminderDays == 7 &&
            item.NotificationCount == 1);
        Assert.DoesNotContain(firstScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.EventType == TenantLifecycleEventTypes.Expired);

        var duplicateScan = await ScanAsync();
        Assert.DoesNotContain(duplicateScan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.EventType == TenantLifecycleEventTypes.ExpiryReminder);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var adminId = await dbContext.Users
            .Where(item => item.UserName == tenant.AdminUserName)
            .Select(item => item.Id)
            .SingleAsync();
        Assert.Equal(1, await dbContext.UserNotifications.CountAsync(item =>
            item.UserId == adminId && item.SourceType == "TenantLifecycle"));
        var reminder = await dbContext.TenantLifecycleRecords.SingleAsync(item =>
            item.TenantId == tenant.Id &&
            item.EventType == TenantLifecycleEventTypes.ExpiryReminder);
        Assert.Equal(7, reminder.ReminderDays);
        Assert.False(string.IsNullOrWhiteSpace(reminder.DeduplicationKey));
    }

    [Fact]
    public async Task ExpiredTenant_InvalidatesToken_AndCanBeRenewedWithHistory()
    {
        await AuthorizePlatformAsync();
        var tenant = await ProvisionTenantAsync(DateTimeOffset.UtcNow.AddDays(30));
        var tenantToken = await LoginAsync(
            tenant.AdminUserName,
            tenant.AdminPassword,
            tenant.Code);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var entity = await dbContext.Tenants.SingleAsync(item => item.Id == tenant.Id);
            entity.ExpireAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            entity.Status = TenantStatus.Active;
            await dbContext.SaveChangesAsync();
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tenantToken);
        var scan = await ScanAsync();
        Assert.Contains(scan.Details, item =>
            item.TenantId == tenant.Id.ToString() &&
            item.EventType == TenantLifecycleEventTypes.Expired);

        var oldTokenResponse = await client.GetAsync("/user/info");
        Assert.Equal(HttpStatusCode.Unauthorized, oldTokenResponse.StatusCode);

        await AuthorizePlatformAsync();
        var newExpireAt = DateTimeOffset.UtcNow.AddYears(1);
        var renewResponse = await client.PostAsJsonAsync(
            $"/platform/tenant/{tenant.Id}/renew",
            new
            {
                expireAt = newExpireAt,
                reactivate = true,
                remark = "集成测试续期"
            });
        renewResponse.EnsureSuccessStatusCode();
        var renewed = await renewResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantDto>>();
        Assert.NotNull(renewed);
        Assert.Equal("Active", renewed.Data.Status);

        var history = await client.GetFromJsonAsync<ApiEnvelope<PageData<TenantLifecycleRecordDto>>>(
            $"/platform/tenant/{tenant.Id}/lifecycle-records?page=1&pageSize=100");
        Assert.NotNull(history);
        Assert.Contains(history.Data.Items, item => item.EventType == TenantLifecycleEventTypes.Created);
        Assert.Contains(history.Data.Items, item => item.EventType == TenantLifecycleEventTypes.Expired);
        Assert.Contains(history.Data.Items, item =>
            item.EventType == TenantLifecycleEventTypes.Renewed &&
            item.OperatorUserName == "admin");

        var newToken = await LoginAsync(
            tenant.AdminUserName,
            tenant.AdminPassword,
            tenant.Code);
        Assert.False(string.IsNullOrWhiteSpace(newToken));
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private async Task<ProvisionedTenant> ProvisionTenantAsync(DateTimeOffset expireAt)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var code = $"lifecycle-{unique}";
        var adminUserName = $"lifecycle-admin-{unique}";
        var adminPassword = $"Lifecycle{unique}1";
        var response = await client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = $"生命周期测试租户 {unique}",
            expireAt,
            adminUserName,
            adminRealName = "生命周期测试管理员",
            adminPassword
        });
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<ApiEnvelope<TenantDto>>();
        Assert.NotNull(created);
        return new ProvisionedTenant(
            Guid.Parse(created.Data.Id),
            code,
            adminUserName,
            adminPassword);
    }

    private async Task<TenantLifecycleScanResult> ScanAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITenantLifecycleService>();
        return await service.ScanAsync();
    }

    private async Task AuthorizePlatformAsync()
    {
        var token = await LoginAsync("admin", "123456", null);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private async Task<string> LoginAsync(string username, string password, string? tenantCode)
    {
        client.DefaultRequestHeaders.Authorization = null;
        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            username,
            password,
            tenantCode
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        Assert.NotNull(login);
        return login.Data.AccessToken;
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);
    private sealed record LoginData(string AccessToken);
    private sealed record PageData<T>(IReadOnlyList<T> Items, int Total);
    private sealed record ProvisionedTenant(
        Guid Id,
        string Code,
        string AdminUserName,
        string AdminPassword);
}
