using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Authorization;
using MiniAdmin.Infrastructure.Persistence;
using MiniAdmin.Platform.Authorization;
using MiniAdmin.Platform.Caching;
using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Tests;

public sealed class PlatformKernelTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> factory;
    private readonly HttpClient client;

    public PlatformKernelTests(WebApplicationFactory<Program> rootFactory)
    {
        factory = rootFactory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:InMemoryDatabaseName"] = $"PlatformKernelTests-{Guid.NewGuid():N}",
                    ["Cache:Provider"] = "Memory",
                    ["RateLimiting:Enabled"] = "false",
                    ["OpenApi:Enabled"] = "true"
                });
            });
        });
        client = factory.CreateClient();
    }

    [Fact]
    public void PageRegistry_Rejects_Duplicate_Permission_Codes()
    {
        var provider = new TestPageProvider(
            CreatePage("page.one", "shared:query"),
            CreatePage("page.two", "shared:query"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PageRegistry([provider]));

        Assert.Contains("Duplicate", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PageRegistry_Rejects_Missing_Parent()
    {
        var page = CreatePage("page.child", "child:query") with
        {
            ParentKey = "page.missing"
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new PageRegistry([new TestPageProvider(page)]));

        Assert.Contains("missing parent", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DynamicApi_Requires_Authentication()
    {
        var response = await client.GetAsync("/platform/metadata/pages");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live", "self")]
    [InlineData("/health/ready", "database")]
    [InlineData("/health/ready", "primary-cache")]
    public async Task HealthEndpoints_Report_Real_Registered_Checks(string path, string expectedCheck)
    {
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Healthy", document.RootElement.GetProperty("status").GetString());
        Assert.True(document.RootElement.GetProperty("checks").TryGetProperty(expectedCheck, out _));
    }

    [Fact]
    public async Task PageRegistry_Synchronizes_Menu_Permission_And_Admin_Grant()
    {
        _ = await client.GetAsync("/health");
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var pageId = PageIdentity.CreateDeterministicGuid("page:platform.kernel");
        var permissionId = PageIdentity.CreateDeterministicGuid(
            "permission:platform:metadata:query");

        var page = await dbContext.Menus.AsNoTracking().SingleAsync(menu => menu.Id == pageId);
        var permission = await dbContext.Menus.AsNoTracking().SingleAsync(
            menu => menu.Id == permissionId);
        var adminRoleId = await dbContext.Roles
            .AsNoTracking()
            .Where(role => role.TenantId == null && role.Code == "admin")
            .Select(role => role.Id)
            .SingleAsync();

        Assert.Equal("/platform-kernel", page.Path);
        Assert.Equal("platform:metadata:query", page.PermissionCode);
        Assert.Equal(pageId, permission.ParentId);
        Assert.Equal("platform:metadata:query", permission.PermissionCode);
        Assert.True(await dbContext.RoleMenus.AsNoTracking().AnyAsync(roleMenu =>
            roleMenu.RoleId == adminRoleId &&
            roleMenu.MenuId == permissionId));
    }

    [Fact]
    public void AbacConditionEvaluator_Supports_Nested_References_And_Cidr()
    {
        const string conditions = """
            {
              "all": [
                { "attribute": "request.method", "operator": "equals", "value": "GET" },
                {
                  "any": [
                    { "attribute": "user.id", "operator": "equals", "value": "$owner.id" },
                    { "attribute": "request.ip", "operator": "ipInCidr", "value": "10.20.0.0/16" }
                  ]
                }
              ]
            }
            """;
        var attributes = new Dictionary<string, object?>
        {
            ["request.method"] = "GET",
            ["user.id"] = "user-1",
            ["owner.id"] = "user-1",
            ["request.ip"] = "192.168.1.10"
        };

        Assert.True(AbacConditionEvaluator.Evaluate(conditions, attributes));
        attributes["owner.id"] = "user-2";
        Assert.False(AbacConditionEvaluator.Evaluate(conditions, attributes));
        attributes["request.ip"] = "10.20.8.9";
        Assert.True(AbacConditionEvaluator.Evaluate(conditions, attributes));
    }

    [Fact]
    public async Task PlatformCache_Uses_Tenant_And_Global_Tag_Version_Gates()
    {
        using var scope = factory.Services.CreateScope();
        var platformCache = scope.ServiceProvider.GetRequiredService<IPlatformCache>();
        var tenantId = Guid.NewGuid();
        var otherTenantId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N");
        var tag = $"test-cache:{suffix}";
        var factoryCalls = 0;

        async Task<string?> CreateValue(CancellationToken _)
        {
            await Task.Yield();
            factoryCalls++;
            return $"value-{factoryCalls}";
        }

        var first = await platformCache.GetOrCreateAsync(
            "test",
            suffix,
            tenantId,
            [tag],
            CreateValue);
        var cached = await platformCache.GetOrCreateAsync(
            "test",
            suffix,
            tenantId,
            [tag],
            CreateValue);
        Assert.Equal("value-1", first);
        Assert.Equal(first, cached);
        Assert.Equal(1, factoryCalls);

        await platformCache.InvalidateTagsAsync(otherTenantId, [tag]);
        _ = await platformCache.GetOrCreateAsync(
            "test",
            suffix,
            tenantId,
            [tag],
            CreateValue);
        Assert.Equal(1, factoryCalls);

        await platformCache.InvalidateTagsAsync(tenantId, [tag]);
        var tenantInvalidated = await platformCache.GetOrCreateAsync(
            "test",
            suffix,
            tenantId,
            [tag],
            CreateValue);
        Assert.Equal("value-2", tenantInvalidated);

        await platformCache.InvalidateTagsAsync(tenantId: null, [tag]);
        var globallyInvalidated = await platformCache.GetOrCreateAsync(
            "test",
            suffix,
            tenantId,
            [tag],
            CreateValue);
        Assert.Equal("value-3", globallyInvalidated);
    }

    [Fact]
    public async Task DynamicApi_Uses_PageRegistry_And_Appears_In_OpenApi()
    {
        await AuthorizeAsync();

        var pagesResponse = await client.GetAsync("/platform/metadata/pages");
        pagesResponse.EnsureSuccessStatusCode();
        var pages = await pagesResponse.Content.ReadFromJsonAsync<PageDefinition[]>();

        Assert.NotNull(pages);
        Assert.Contains(pages, page => page.Key == "platform.kernel");

        var openApi = await client.GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(openApi);
        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/platform/metadata/pages", out var pagePath));
        Assert.True(pagePath.TryGetProperty("get", out var operation));
        Assert.Equal("GetPlatformPages", operation.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task PageRegistry_menu_titles_follow_accept_language()
    {
        await AuthorizeAsync();

        using var englishRequest = new HttpRequestMessage(HttpMethod.Get, "/system/menu/list");
        englishRequest.Headers.AcceptLanguage.ParseAdd("en-US");
        using var englishResponse = await client.SendAsync(englishRequest);
        englishResponse.EnsureSuccessStatusCode();
        using var englishDocument = JsonDocument.Parse(await englishResponse.Content.ReadAsStringAsync());

        using var chineseRequest = new HttpRequestMessage(HttpMethod.Get, "/system/menu/list");
        chineseRequest.Headers.AcceptLanguage.ParseAdd("zh-CN");
        using var chineseResponse = await client.SendAsync(chineseRequest);
        chineseResponse.EnsureSuccessStatusCode();
        using var chineseDocument = JsonDocument.Parse(await chineseResponse.Content.ReadAsStringAsync());

        Assert.Equal("Platform Kernel", FindMenuTitle(
            englishDocument.RootElement.GetProperty("data"),
            "platform.kernel"));
        Assert.Equal("平台内核", FindMenuTitle(
            chineseDocument.RootElement.GetProperty("data"),
            "platform.kernel"));
    }

    [Fact]
    public async Task Abac_Deny_Policy_Overrides_Rbac_For_Dynamic_Api()
    {
        await AuthorizeAsync();
        var createResponse = await client.PostAsJsonAsync(
            "/platform/abac-policies",
            new SaveAbacPolicyRequest(
                TenantId: null,
                Name: "测试拒绝元数据读取",
                SubjectType: "Any",
                SubjectId: null,
                Resource: "platform.metadata",
                Action: "query",
                Effect: "Deny",
                ConditionsJson: """
                    { "attribute": "request.method", "operator": "equals", "value": "GET" }
                    """,
                Priority: 1000,
                IsEnabled: true,
                Description: "integration test"));
        createResponse.EnsureSuccessStatusCode();
        var policy = await createResponse.Content.ReadFromJsonAsync<AbacPolicyDto>();
        Assert.NotNull(policy);

        try
        {
            var deniedResponse = await client.GetAsync("/platform/metadata/pages");
            Assert.Equal(HttpStatusCode.Forbidden, deniedResponse.StatusCode);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var storedPolicy = await dbContext.AbacPolicies.FindAsync(policy.Id);
            if (storedPolicy is not null)
            {
                dbContext.AbacPolicies.Remove(storedPolicy);
                await dbContext.SaveChangesAsync();
            }
        }
    }

    public void Dispose()
    {
        client.Dispose();
        factory.Dispose();
    }

    private async Task AuthorizeAsync()
    {
        var response = await client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "123456"
        });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        Assert.NotNull(login);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.Data.AccessToken);
    }

    private static PageDefinition CreatePage(string key, string permissionCode)
    {
        return new PageDefinition(
            Key: key,
            ParentKey: null,
            Path: $"/{key}",
            Component: null,
            Redirect: null,
            Icon: "lucide:box",
            Order: 1,
            I18nKey: $"page.{key}",
            Title: new LocalizedText(key, key),
            IsVisible: false,
            Permissions:
            [
                new PermissionDefinition(
                    permissionCode,
                    key,
                    "query",
                    $"permission.{key}.query",
                    new LocalizedText("查询", "Query"))
            ]);
    }

    private static string? FindMenuTitle(JsonElement menus, string name)
    {
        foreach (var menu in menus.EnumerateArray())
        {
            if (string.Equals(menu.GetProperty("name").GetString(), name, StringComparison.OrdinalIgnoreCase))
            {
                return menu.GetProperty("title").GetString();
            }

            if (menu.TryGetProperty("children", out var children))
            {
                var title = FindMenuTitle(children, name);
                if (title is not null)
                {
                    return title;
                }
            }
        }

        return null;
    }

    private sealed class TestPageProvider(params PageDefinition[] pages) : IPageDefinitionProvider
    {
        public IEnumerable<PageDefinition> GetPages()
        {
            return pages;
        }
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);

    private sealed record LoginData(string AccessToken);
}
