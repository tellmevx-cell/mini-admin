using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class TenantResourceQuotaTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient client;
    private readonly WebApplicationFactory<Program> factory;
    private readonly string storageRoot;

    public TenantResourceQuotaTests(WebApplicationFactory<Program> rootFactory)
    {
        storageRoot = Path.Combine(Path.GetTempPath(), $"mini-admin-quota-{Guid.NewGuid():N}");
        factory = rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:InMemoryDatabaseName"] = $"MiniAdminQuotaTests-{Guid.NewGuid():N}",
                    ["Cache:Provider"] = "Memory",
                    ["Cache:Redis:Configuration"] = string.Empty,
                    ["RateLimiting:Enabled"] = "false",
                    ["FileStorage:Provider"] = "Local",
                    ["FileStorage:Local:RootPath"] = storageRoot
                });
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public async Task TenantResourceQuota_UserCreationAndImport_AreEnforcedUnderConcurrency()
    {
        var tenant = await ProvisionTenantAsync(maxUsers: 2, maxStorageMb: 0);
        await AuthorizeAsync(tenant.AdminUserName, tenant.AdminPassword, tenant.Code);

        var firstRequest = CreateUserRequest($"quota-user-a-{tenant.Unique}");
        var secondRequest = CreateUserRequest($"quota-user-b-{tenant.Unique}");
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/system/user", firstRequest),
            client.PostAsJsonAsync("/system/user", secondRequest));

        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        var successfulResponse = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var created = await successfulResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>();
        Assert.NotNull(created);

        var deleteResponse = await client.DeleteAsync($"/system/user/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var replacementResponse = await client.PostAsJsonAsync(
            "/system/user",
            CreateUserRequest($"quota-user-c-{tenant.Unique}"));
        replacementResponse.EnsureSuccessStatusCode();

        var workbook = CreateImportWorkbook(
            $"quota-import-a-{tenant.Unique}",
            $"quota-import-b-{tenant.Unique}");
        using var previewContent = CreateWorkbookContent(workbook);
        var previewResponse = await client.PostAsync("/system/user/import/preview", previewContent);
        Assert.Equal(HttpStatusCode.Conflict, previewResponse.StatusCode);

        using var importContent = CreateWorkbookContent(workbook);
        var importResponse = await client.PostAsync("/system/user/import", importContent);
        Assert.Equal(HttpStatusCode.Conflict, importResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var tenantUserCount = await dbContext.Users.CountAsync(x => x.TenantId == tenant.Id);
        Assert.Equal(2, tenantUserCount);
    }

    [Fact]
    public async Task TenantResourceQuota_FileQuota_ReleasesOnDeleteAndIsTenantScoped()
    {
        var firstTenant = await ProvisionTenantAsync(maxUsers: 0, maxStorageMb: 1);
        await AuthorizeAsync(firstTenant.AdminUserName, firstTenant.AdminPassword, firstTenant.Code);

        var firstUpload = await UploadAsync(
            $"quota-first-{firstTenant.Unique}.bin",
            new byte[700 * 1024]);
        firstUpload.EnsureSuccessStatusCode();
        var firstFile = await firstUpload.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();
        Assert.NotNull(firstFile);

        var rejectedUpload = await UploadAsync(
            $"quota-rejected-{firstTenant.Unique}.bin",
            new byte[400 * 1024]);
        Assert.Equal(HttpStatusCode.Conflict, rejectedUpload.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var storedFile = await dbContext.ManagedFiles.SingleAsync(x => x.Id == Guid.Parse(firstFile.Data.Id));
            Assert.Equal(firstTenant.Id, storedFile.TenantId);
            Assert.False(await dbContext.ManagedFiles.AnyAsync(
                x => x.OriginalName == $"quota-rejected-{firstTenant.Unique}.bin"));
        }

        var secondTenant = await ProvisionTenantAsync(maxUsers: 0, maxStorageMb: 1);
        await AuthorizeAsync(secondTenant.AdminUserName, secondTenant.AdminPassword, secondTenant.Code);

        var secondTenantList = await client.GetFromJsonAsync<ApiEnvelope<PageData<FileData>>>(
            $"/system/file/list?page=1&pageSize=20&originalName={Uri.EscapeDataString(firstFile.Data.OriginalName)}");
        Assert.NotNull(secondTenantList);
        Assert.Empty(secondTenantList.Data.Items);

        var crossTenantDownload = await client.GetAsync($"/system/file/{firstFile.Data.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantDownload.StatusCode);

        await AuthorizeAsync(firstTenant.AdminUserName, firstTenant.AdminPassword, firstTenant.Code);
        var deleteResponse = await client.DeleteAsync($"/system/file/{firstFile.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();

        var uploadAfterDelete = await UploadAsync(
            $"quota-after-delete-{firstTenant.Unique}.bin",
            new byte[400 * 1024]);
        uploadAfterDelete.EnsureSuccessStatusCode();
        var replacementFile = await uploadAfterDelete.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();
        Assert.NotNull(replacementFile);

        await AuthorizeAsync("admin", "123456", null);
        var tenantList = await client.GetFromJsonAsync<ApiEnvelope<PageData<TenantUsageData>>>(
            $"/platform/tenant/list?page=1&pageSize=10&code={firstTenant.Code}");
        Assert.NotNull(tenantList);
        var usage = Assert.Single(tenantList.Data.Items);
        Assert.Equal(1, usage.UsedUsers);
        Assert.Equal(0, usage.MaxUsers);
        Assert.Equal(400 * 1024, usage.UsedStorageBytes);
        Assert.Equal(1024 * 1024, usage.MaxStorageBytes);

        await AuthorizeAsync(firstTenant.AdminUserName, firstTenant.AdminPassword, firstTenant.Code);
        await client.DeleteAsync($"/system/file/{replacementFile.Data.Id}");
    }

    [Fact]
    public async Task TenantResourceQuota_ZeroLimits_AllowTenantWrites()
    {
        var tenant = await ProvisionTenantAsync(maxUsers: 0, maxStorageMb: 0);
        await AuthorizeAsync(tenant.AdminUserName, tenant.AdminPassword, tenant.Code);

        var createUserResponse = await client.PostAsJsonAsync(
            "/system/user",
            CreateUserRequest($"unlimited-user-{tenant.Unique}"));
        createUserResponse.EnsureSuccessStatusCode();

        var uploadResponse = await UploadAsync(
            $"unlimited-file-{tenant.Unique}.bin",
            new byte[2 * 1024 * 1024]);
        uploadResponse.EnsureSuccessStatusCode();
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();
        Assert.NotNull(uploaded);

        await client.DeleteAsync($"/system/file/{uploaded.Data.Id}");
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();

        try
        {
            if (Directory.Exists(storageRoot))
            {
                Directory.Delete(storageRoot, recursive: true);
            }
        }
        catch
        {
            // Test cleanup must not hide the assertion result.
        }
    }

    private async Task<ProvisionedTenant> ProvisionTenantAsync(int maxUsers, int maxStorageMb)
    {
        await AuthorizeAsync("admin", "123456", null);
        var unique = Guid.NewGuid().ToString("N")[..8];
        var packageId = Guid.NewGuid();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var tenantAdminRoleId = await dbContext.Roles
                .Where(x => x.Code == "tenant-admin")
                .Select(x => x.Id)
                .SingleAsync();
            var extraPermissionCodes = new[]
            {
                "system:user:import",
                "system:file:query",
                "system:file:upload",
                "system:file:download",
                "system:file:delete",
                "system:file:mark-invalid"
            };
            var extraMenuIds = await dbContext.Menus
                .Where(x => x.Name == "FileManagement" ||
                            (x.PermissionCode != null && extraPermissionCodes.Contains(x.PermissionCode)))
                .Select(x => x.Id)
                .ToArrayAsync();
            var existingRoleMenuIds = await dbContext.RoleMenus
                .Where(x => x.RoleId == tenantAdminRoleId)
                .Select(x => x.MenuId)
                .ToListAsync();

            foreach (var menuId in extraMenuIds.Where(menuId => !existingRoleMenuIds.Contains(menuId)))
            {
                dbContext.RoleMenus.Add(new RoleMenu
                {
                    RoleId = tenantAdminRoleId,
                    MenuId = menuId
                });
                existingRoleMenuIds.Add(menuId);
            }

            dbContext.TenantPackages.Add(new TenantPackage
            {
                Id = packageId,
                Name = $"Quota package {unique}",
                MaxUsers = maxUsers,
                MaxStorageMb = maxStorageMb,
                MenuIds = JsonSerializer.Serialize(existingRoleMenuIds.Distinct().ToArray()),
                IsEnabled = true
            });
            await dbContext.SaveChangesAsync();
        }

        var code = $"quota-{unique}";
        var adminUserName = $"quota-admin-{unique}";
        var adminPassword = $"Quota{unique}1";
        var response = await client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = $"配额测试租户 {unique}",
            packageId,
            initializationTemplateCode = "standard",
            adminUserName,
            adminRealName = "配额测试管理员",
            adminPassword
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var tenant = await response.Content.ReadFromJsonAsync<ApiEnvelope<TenantCreatedData>>();
        Assert.NotNull(tenant);

        return new ProvisionedTenant(
            Guid.Parse(tenant.Data.Id),
            code,
            adminUserName,
            adminPassword,
            unique);
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

    private byte[] CreateImportWorkbook(params string[] userNames)
    {
        using var scope = factory.Services.CreateScope();
        var workbookService = scope.ServiceProvider.GetRequiredService<IUserImportExportService>();
        var rows = new List<IReadOnlyList<string>>
        {
            new[]
            {
                "用户名",
                "姓名",
                "初始密码",
                "部门编码",
                "岗位编码",
                "角色编码",
                "启用状态"
            }
        };
        rows.AddRange(userNames.Select(userName => (IReadOnlyList<string>)new[]
        {
            userName,
            "配额导入用户",
            "Import123",
            string.Empty,
            string.Empty,
            string.Empty,
            "启用"
        }));
        return workbookService.CreateWorkbook(rows);
    }

    private static MultipartFormDataContent CreateWorkbookContent(byte[] workbook)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(workbook);
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(file, "file", "quota-users.xlsx");
        return content;
    }

    private Task<HttpResponseMessage> UploadAsync(string fileName, byte[] bytes)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(file, "file", fileName);
        return client.PostAsync("/system/file/upload", content);
    }

    private static object CreateUserRequest(string userName)
    {
        return new
        {
            userName,
            realName = "配额测试用户",
            password = "Quota123",
            departmentId = (Guid?)null,
            positionId = (Guid?)null,
            roleIds = Array.Empty<Guid>(),
            isEnabled = true
        };
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);
    private sealed record LoginData(string AccessToken);
    private sealed record UserData(string Id, string UserName);
    private sealed record FileData(string Id, string OriginalName, long Size);
    private sealed record TenantCreatedData(string Id, string Code);
    private sealed record TenantUsageData(
        string Id,
        int UsedUsers,
        int MaxUsers,
        long UsedStorageBytes,
        long MaxStorageBytes);
    private sealed record PageData<T>(T[] Items, int Total);
    private sealed record ProvisionedTenant(
        Guid Id,
        string Code,
        string AdminUserName,
        string AdminPassword,
        string Unique);
}
