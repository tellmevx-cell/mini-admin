using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using MiniAdmin.Application.Contracts.Workflows;
using MiniAdmin.Application.Contracts.Caching;
using MiniAdmin.Application.Contracts.DataScopes;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class VbenLoginLoopTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public VbenLoginLoopTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Database:Provider"] = "InMemory",
                    ["Database:InMemoryDatabaseName"] = $"MiniAdminTests-{Guid.NewGuid():N}",
                    ["Cache:Provider"] = "Memory",
                    ["Cache:Redis:Configuration"] = string.Empty
                });
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Login_Returns_AccessToken_In_Vben_Response_Wrapper()
    {
        var response = await LoginAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal("ok", json.Message);
        Assert.False(string.IsNullOrWhiteSpace(json.Data.AccessToken));
        Assert.Equal(3, json.Data.AccessToken.Split('.').Length);
    }

    [Fact]
    public async Task TenantLogin_With_Valid_TenantCode_Returns_Tenant_Info()
    {
        var response = await LoginAsync("demo", "123456", "demo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        Assert.NotNull(json);
        Assert.False(string.IsNullOrWhiteSpace(json.Data.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(json.Data.TenantId));
        Assert.Equal("demo", json.Data.TenantCode);
    }

    [Fact]
    public async Task TenantLogin_Rejects_Tenant_User_Without_TenantCode()
    {
        var response = await LoginAsync("demo", "123456");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantLogin_Rejects_Disabled_Tenant()
    {
        await SetDemoTenantStatusAsync(TenantStatus.Disabled);

        var response = await LoginAsync("demo", "123456", "demo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantLogin_Disabled_Tenant_Invalidates_Previous_Token()
    {
        var login = await LoginAsync("demo", "123456", "demo");
        login.EnsureSuccessStatusCode();
        var json = await login.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        Assert.NotNull(json);

        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.Data.AccessToken);

        await SetDemoTenantStatusAsync(TenantStatus.Disabled);

        var userInfo = await _client.GetAsync("/user/info");

        Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
    }

    [Fact]
    public async Task PlatformTenant_List_Returns_Default_Demo_Tenant()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<TenantData>>>(
            "/platform/tenant/list?page=1&pageSize=10&code=demo");

        Assert.NotNull(json);
        var tenant = Assert.Single(json.Data.Items);
        Assert.Equal("demo", tenant.Code);
        Assert.Equal("演示租户", tenant.Name);
        Assert.False(string.IsNullOrWhiteSpace(tenant.Status));
    }

    [Fact]
    public async Task TenantLoginOptions_Returns_Active_Tenants_For_Login_Page()
    {
        var json = await _client.GetFromJsonAsync<ApiEnvelope<TenantLoginOptionData[]>>(
            "/auth/tenant-options");

        Assert.NotNull(json);
        Assert.Contains(json.Data, x => x.Code == "demo" && x.Name == "演示租户");
    }

    [Fact]
    public async Task PlatformTenant_List_Returns_Expired_When_Active_Tenant_ExpireAt_Passed()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var code = $"expired-list-{unique}";
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            dbContext.Tenants.Add(new Tenant
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = "过期列表测试租户",
                Status = TenantStatus.Active,
                InitializationStatus = "Success",
                ExpireAt = DateTimeOffset.UtcNow.AddMinutes(-1),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<TenantData>>>(
            $"/platform/tenant/list?page=1&pageSize=10&code={code}");

        Assert.NotNull(json);
        var tenant = Assert.Single(json.Data.Items);
        Assert.Equal("Expired", tenant.Status);

        var expiredFilter = await _client.GetFromJsonAsync<ApiEnvelope<PageData<TenantData>>>(
            "/platform/tenant/list?page=1&pageSize=10&status=Expired");
        Assert.NotNull(expiredFilter);
        Assert.Contains(expiredFilter.Data.Items, x => x.Code == code);

        var activeFilter = await _client.GetFromJsonAsync<ApiEnvelope<PageData<TenantData>>>(
            "/platform/tenant/list?page=1&pageSize=10&status=Active");
        Assert.NotNull(activeFilter);
        Assert.DoesNotContain(activeFilter.Data.Items, x => x.Code == code);
    }

    [Fact]
    public async Task PlatformTenant_Create_Rejects_Expired_ExpireAt()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = $"expired-{unique}",
            name = "过期时间测试租户",
            expireAt = DateTimeOffset.UtcNow.AddMinutes(-1),
            adminUserName = $"expired-admin-{unique}",
            adminRealName = "过期测试管理员",
            adminPassword = $"Tenant{unique}1"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<TenantData?>>();
        Assert.NotNull(json);
        Assert.Contains("到期时间", json.Message);
    }

    [Fact]
    public async Task PlatformTenant_Update_Rejects_Expired_ExpireAt()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var code = $"update-expired-{unique}";

        var createResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = "编辑过期时间租户",
            adminUserName = $"update-expired-admin-{unique}",
            adminRealName = "编辑过期测试管理员",
            adminPassword = $"Tenant{unique}1"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(created);

        var updateResponse = await _client.PutAsJsonAsync($"/platform/tenant/{created.Data.Id}", new
        {
            name = "编辑过期时间租户",
            expireAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
        var json = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData?>>();
        Assert.NotNull(json);
        Assert.Contains("到期时间", json.Message);
    }

    [Fact]
    public async Task PlatformTenant_Can_Create_Update_And_Disable_Tenant()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var code = $"tenant-{unique}";
        var adminUserName = $"tenant-admin-crud-{unique}";

        var createResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = "测试租户",
            contactName = "张三",
            contactPhone = "13800000000",
            contactEmail = "tenant@example.com",
            remark = "created from test",
            adminUserName,
            adminRealName = "租户管理员",
            adminEmail = "tenant-admin-crud@example.com",
            adminPassword = $"Tenant{unique}1"
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();

        Assert.NotNull(created);
        Assert.Equal(code, created.Data.Code);
        Assert.Equal("Active", created.Data.Status);

        var updateResponse = await _client.PutAsJsonAsync($"/platform/tenant/{created.Data.Id}", new
        {
            name = "测试租户更新",
            contactName = "李四",
            contactPhone = "13900000000",
            contactEmail = "updated@example.com",
            expireAt = DateTimeOffset.UtcNow.AddDays(30),
            remark = "updated from test"
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();

        Assert.NotNull(updated);
        Assert.Equal("测试租户更新", updated.Data.Name);
        Assert.Equal("李四", updated.Data.ContactName);

        var disableResponse = await _client.PostAsync($"/platform/tenant/{created.Data.Id}/disable", null);
        disableResponse.EnsureSuccessStatusCode();
        var disabled = await disableResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();

        Assert.NotNull(disabled);
        Assert.Equal("Disabled", disabled.Data.Status);
    }

    [Fact]
    public async Task TenantAdmin_CreateTenant_Creates_Admin_And_Allows_Login()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var code = $"open-{unique}";
        var adminUserName = $"tenant-admin-{unique}";
        var adminPassword = $"Tenant{unique}1";

        var createResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code,
            name = "开通测试租户",
            contactName = "租户联系人",
            contactPhone = "13800000001",
            contactEmail = "open-tenant@example.com",
            adminUserName,
            adminRealName = "租户管理员",
            adminEmail = "tenant-admin@example.com",
            adminPassword
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(created);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var tenantId = Guid.Parse(created.Data.Id);
            var adminUser = await dbContext.Users
                .Include(x => x.UserRoles)
                .ThenInclude(x => x.Role)
                .SingleOrDefaultAsync(x => x.UserName == adminUserName);

            Assert.NotNull(adminUser);
            Assert.Equal(tenantId, adminUser.TenantId);
            Assert.Equal("租户管理员", adminUser.RealName);
            Assert.Equal("tenant-admin@example.com", adminUser.Email);
            Assert.Contains(adminUser.UserRoles, userRole => userRole.Role.Code == "tenant-admin");
        }

        _client.DefaultRequestHeaders.Authorization = null;
        var loginResponse = await LoginAsync(adminUserName, adminPassword, code);
        loginResponse.EnsureSuccessStatusCode();
        var login = await loginResponse.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        Assert.NotNull(login);
        Assert.Equal(code, login.Data.TenantCode);
        Assert.False(string.IsNullOrWhiteSpace(login.Data.AccessToken));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            login.Data.AccessToken);
        var menus = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(menus);
        var dashboard = Assert.Single(menus.Data, menu => menu.Name == "Dashboard");
        Assert.Contains(dashboard.Children, menu => menu.Name == "Analytics");
    }

    [Fact]
    public async Task TenantAdmin_CreateTenant_Rejects_Duplicate_Admin_UserName()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var createResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = $"duplicate-admin-{unique}",
            name = "重复管理员测试租户",
            adminUserName = "admin",
            adminRealName = "重复管理员",
            adminPassword = "TenantAdmin123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, createResponse.StatusCode);
    }

    [Fact]
    public async Task Tenant_Create_Initializes_Standard_Template_Foundation_Data()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantCode = $"tmpl-{unique}";
        var adminUserName = $"tenant-tmpl-{unique}";

        var createTenantResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = tenantCode,
            name = "模板初始化租户",
            initializationTemplateCode = "standard",
            adminUserName,
            adminRealName = "模板租户管理员",
            adminPassword = $"Tenant{unique}1"
        });
        createTenantResponse.EnsureSuccessStatusCode();
        var createdTenant = await createTenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(createdTenant);
        Assert.Equal("standard", createdTenant.Data.InitializationTemplateCode);
        Assert.Equal("Success", createdTenant.Data.InitializationStatus);
        Assert.NotNull(createdTenant.Data.InitializedAt);
        Assert.Null(createdTenant.Data.InitializationError);

        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var tenantId = Guid.Parse(createdTenant.Data.Id);

        var departmentCodes = await dbContext.Departments
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Code)
            .ToArrayAsync();
        Assert.Contains("HQ", departmentCodes);
        Assert.Contains("RD", departmentCodes);
        Assert.Contains("MKT", departmentCodes);

        var positionCodes = await dbContext.Positions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => x.Code)
            .ToArrayAsync();
        Assert.Contains("dept-lead", positionCodes);
        Assert.Contains("developer", positionCodes);
        Assert.Contains("sales-manager", positionCodes);

        var adminUser = await dbContext.Users
            .AsNoTracking()
            .SingleAsync(x => x.UserName == adminUserName);
        Assert.NotNull(adminUser.DepartmentId);
        Assert.NotNull(adminUser.PositionId);

        var employeeRole = await dbContext.Roles
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Code == "employee");
        Assert.NotNull(employeeRole);

        var employeePermissionCodes = await dbContext.RoleMenus
            .AsNoTracking()
            .Where(x => x.RoleId == employeeRole.Id)
            .Select(x => x.Menu.PermissionCode)
            .Where(x => x != null)
            .ToArrayAsync();
        Assert.Contains("system:user:query", employeePermissionCodes);
        Assert.Contains("system:department:query", employeePermissionCodes);
        Assert.DoesNotContain("system:user:delete", employeePermissionCodes);
    }

    [Fact]
    public async Task TenantDataIsolation_TenantAdmin_SeesOnlyOwnCoreData_AndCreatesTenantUser()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantCode = $"iso-{unique}";
        var tenantAdminUserName = $"tenant-admin-iso-{unique}";
        var tenantAdminPassword = $"Tenant{unique}1";

        var createTenantResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = tenantCode,
            name = "隔离测试租户",
            adminUserName = tenantAdminUserName,
            adminRealName = "隔离租户管理员",
            adminPassword = tenantAdminPassword
        });
        createTenantResponse.EnsureSuccessStatusCode();
        var createdTenant = await createTenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(createdTenant);
        var tenantId = Guid.Parse(createdTenant.Data.Id);

        await AuthorizeAsync(tenantAdminUserName, tenantAdminPassword, tenantCode);

        var users = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=100");
        Assert.NotNull(users);
        Assert.Contains(users.Data.Items, x => x.UserName == tenantAdminUserName);
        Assert.DoesNotContain(users.Data.Items, x => x.UserName == "admin");
        Assert.DoesNotContain(users.Data.Items, x => x.UserName == "demo");

        var roles = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=100");
        Assert.NotNull(roles);
        Assert.DoesNotContain(roles.Data.Items, x => x.Code == "admin");

        var departments = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        Assert.NotNull(departments);
        var flatDepartments = FlattenDepartments(departments.Data).ToArray();
        Assert.DoesNotContain(flatDepartments, x => x.Code == "hq");
        Assert.DoesNotContain(flatDepartments, x => x.Code == "rd");

        var positions = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=100");
        Assert.NotNull(positions);
        Assert.DoesNotContain(positions.Data.Items, x => x.Code == "manager");
        Assert.Contains(positions.Data.Items, x => x.Code == "developer");

        var createDepartmentResponse = await _client.PostAsJsonAsync("/system/department", new
        {
            parentId = (Guid?)null,
            code = $"dept-{unique}",
            name = "隔离部门",
            leader = "租户负责人",
            phone = "13800009999",
            order = 1,
            isEnabled = true
        });
        createDepartmentResponse.EnsureSuccessStatusCode();
        var createdDepartment = await createDepartmentResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();
        Assert.NotNull(createdDepartment);

        var createPositionResponse = await _client.PostAsJsonAsync("/system/position", new
        {
            code = $"pos-{unique}",
            name = "隔离岗位",
            order = 1,
            remark = "tenant position",
            isEnabled = true
        });
        createPositionResponse.EnsureSuccessStatusCode();
        var createdPosition = await createPositionResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionData>>();
        Assert.NotNull(createdPosition);

        var createRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = $"role-{unique}",
            name = "隔离角色",
            isEnabled = true,
            dataScope = "self"
        });
        createRoleResponse.EnsureSuccessStatusCode();
        var createdRole = await createRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();
        Assert.NotNull(createdRole);

        var createdUserName = $"tenant-user-{unique}";
        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName = createdUserName,
            realName = "租户普通用户",
            email = "tenant-user@example.com",
            password = $"User{unique}1",
            departmentId = Guid.Parse(createdDepartment.Data.Id),
            positionId = Guid.Parse(createdPosition.Data.Id),
            roleIds = new[] { Guid.Parse(createdRole.Data.Id) },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var createdUser = await dbContext.Users
            .Include(x => x.UserRoles)
            .SingleAsync(x => x.UserName == createdUserName);
        var department = await dbContext.Departments.SingleAsync(x => x.Id == Guid.Parse(createdDepartment.Data.Id));
        var position = await dbContext.Positions.SingleAsync(x => x.Id == Guid.Parse(createdPosition.Data.Id));
        var role = await dbContext.Roles.SingleAsync(x => x.Id == Guid.Parse(createdRole.Data.Id));

        Assert.Equal(tenantId, createdUser.TenantId);
        Assert.Equal(tenantId, dbContext.Entry(department).Property<Guid?>("TenantId").CurrentValue);
        Assert.Equal(tenantId, dbContext.Entry(position).Property<Guid?>("TenantId").CurrentValue);
        Assert.Equal(tenantId, dbContext.Entry(role).Property<Guid?>("TenantId").CurrentValue);
        Assert.Contains(createdUser.UserRoles, x => x.RoleId == role.Id);

        static IEnumerable<DepartmentItemData> FlattenDepartments(IEnumerable<DepartmentItemData> departments)
        {
            foreach (var department in departments)
            {
                yield return department;

                foreach (var child in FlattenDepartments(department.Children))
                {
                    yield return child;
                }
            }
        }
    }

    [Fact]
    public async Task TenantPackageAuthorization_TenantUserPermissions_AreCappedByPackage()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantCode = $"pkg-{unique}";
        var tenantAdminUserName = $"tenant-admin-pkg-{unique}";
        var tenantAdminPassword = $"Tenant{unique}1";

        var createTenantResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = tenantCode,
            name = "套餐权限测试租户",
            adminUserName = tenantAdminUserName,
            adminRealName = "套餐租户管理员",
            adminPassword = tenantAdminPassword
        });
        createTenantResponse.EnsureSuccessStatusCode();
        var createdTenant = await createTenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(createdTenant);
        var tenantId = Guid.Parse(createdTenant.Data.Id);

        await AuthorizeAsync(tenantAdminUserName, tenantAdminPassword, tenantCode);

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = $"pkg-role-{unique}",
            name = "套餐越权角色",
            isEnabled = true,
            dataScope = "self"
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();
        Assert.NotNull(role);

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        Assert.NotNull(menuTree);
        var systemNode = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userNode = Assert.Single(systemNode.Children, menu => menu.Name == "UserManagement");
        var roleNode = Assert.Single(systemNode.Children, menu => menu.Name == "RoleManagement");
        var userQueryNode = Assert.Single(userNode.Children, menu => menu.Name == "UserQueryPermission");
        var roleQueryNode = Assert.Single(roleNode.Children, menu => menu.Name == "RoleQueryPermission");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemNode.Id, userNode.Id, userQueryNode.Id, roleNode.Id, roleQueryNode.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var tenantUserName = $"pkg-user-{unique}";
        var tenantUserPassword = $"User{unique}1";
        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName = tenantUserName,
            realName = "套餐普通用户",
            email = "pkg-user@example.com",
            password = tenantUserPassword,
            roleIds = new[] { Guid.Parse(role.Data.Id) },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();

        await using (var packageScope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = packageScope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var systemMenu = await dbContext.Menus.SingleAsync(x => x.Name == "System");
            var userMenu = await dbContext.Menus.SingleAsync(x => x.Name == "UserManagement");
            var userQueryPermission = await dbContext.Menus.SingleAsync(x => x.Name == "UserQueryPermission");
            var package = new TenantPackage
            {
                Id = Guid.NewGuid(),
                Name = $"基础套餐-{unique}",
                MaxUsers = 100,
                MaxStorageMb = 1024,
                MenuIds = JsonSerializer.Serialize(new[]
                {
                    systemMenu.Id,
                    userMenu.Id,
                    userQueryPermission.Id
                }),
                IsEnabled = true
            };
            dbContext.TenantPackages.Add(package);
            var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == tenantId);
            tenant.PackageId = package.Id;
            var tenantUsers = await dbContext.Users
                .Where(x => x.TenantId == tenantId)
                .ToArrayAsync();
            foreach (var tenantUser in tenantUsers)
            {
                tenantUser.SecurityStamp = Guid.NewGuid().ToString("N");
            }
            await dbContext.SaveChangesAsync();
        }

        await AuthorizeAsync(tenantUserName, tenantUserPassword, tenantCode);
        var menus = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");
        Assert.NotNull(menus);
        var system = Assert.Single(menus.Data, menu => menu.Name == "System");

        Assert.Contains(system.Children, menu => menu.Name == "UserManagement");
        Assert.DoesNotContain(system.Children, menu => menu.Name == "RoleManagement");
    }

    [Fact]
    public async Task TenantPackageAuthorization_ShrinkingPackageMenus_RemovesTenantRoleMenus()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantCode = $"pkg-clean-{unique}";
        var tenantAdminUserName = $"tenant-admin-clean-{unique}";
        var tenantAdminPassword = $"Tenant{unique}1";

        var createTenantResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = tenantCode,
            name = "套餐清理测试租户",
            adminUserName = tenantAdminUserName,
            adminRealName = "套餐清理管理员",
            adminPassword = tenantAdminPassword
        });
        createTenantResponse.EnsureSuccessStatusCode();
        var createdTenant = await createTenantResponse.Content.ReadFromJsonAsync<ApiEnvelope<TenantData>>();
        Assert.NotNull(createdTenant);
        var tenantId = Guid.Parse(createdTenant.Data.Id);

        Guid packageId;
        Guid roleManagementMenuId;
        Guid roleQueryPermissionId;
        string[] allowedMenuIds;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var systemMenu = await dbContext.Menus.SingleAsync(x => x.Name == "System");
            var userMenu = await dbContext.Menus.SingleAsync(x => x.Name == "UserManagement");
            var userQueryPermission = await dbContext.Menus.SingleAsync(x => x.Name == "UserQueryPermission");
            var roleMenu = await dbContext.Menus.SingleAsync(x => x.Name == "RoleManagement");
            var roleQueryPermission = await dbContext.Menus.SingleAsync(x => x.Name == "RoleQueryPermission");
            var roleCreatePermission = await dbContext.Menus.SingleAsync(x => x.Name == "RoleCreatePermission");
            var roleAssignPermission = await dbContext.Menus.SingleAsync(x => x.Name == "RoleAssignPermission");
            packageId = Guid.NewGuid();
            roleManagementMenuId = roleMenu.Id;
            roleQueryPermissionId = roleQueryPermission.Id;
            allowedMenuIds =
            [
                systemMenu.Id.ToString(),
                userMenu.Id.ToString(),
                userQueryPermission.Id.ToString()
            ];

            dbContext.TenantPackages.Add(new TenantPackage
            {
                Id = packageId,
                Name = $"清理套餐-{unique}",
                MaxUsers = 100,
                MaxStorageMb = 1024,
                MenuIds = JsonSerializer.Serialize(new[]
                {
                    systemMenu.Id,
                    userMenu.Id,
                    userQueryPermission.Id,
                    roleMenu.Id,
                    roleQueryPermission.Id,
                    roleCreatePermission.Id,
                    roleAssignPermission.Id
                }),
                IsEnabled = true
            });
            var tenant = await dbContext.Tenants.SingleAsync(x => x.Id == tenantId);
            tenant.PackageId = packageId;
            await dbContext.SaveChangesAsync();
        }

        await AuthorizeAsync(tenantAdminUserName, tenantAdminPassword, tenantCode);
        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = $"clean-role-{unique}",
            name = "套餐清理角色",
            isEnabled = true,
            dataScope = "self"
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();
        Assert.NotNull(role);

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = allowedMenuIds
                .Concat([roleManagementMenuId.ToString(), roleQueryPermissionId.ToString()])
                .ToArray()
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        await AuthorizeAsync();
        var shrinkResponse = await _client.PutAsJsonAsync($"/platform/tenant-package/{packageId}/menus", new
        {
            menuIds = allowedMenuIds
        });
        shrinkResponse.EnsureSuccessStatusCode();

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var roleId = Guid.Parse(role.Data.Id);
        Assert.False(await verifyDbContext.RoleMenus.AnyAsync(
            x => x.RoleId == roleId && x.MenuId == roleManagementMenuId));
        Assert.False(await verifyDbContext.RoleMenus.AnyAsync(
            x => x.RoleId == roleId && x.MenuId == roleQueryPermissionId));
    }

    [Fact]
    public async Task DatabaseInitializer_Restores_Default_Tenant_When_Baseline_Seed_Already_Applied()
    {
        var demoTenantId = Guid.Parse("11000000-0000-0000-0000-000000000001");
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var demoUser = await dbContext.Users.SingleAsync(x => x.UserName == "demo");
        demoUser.TenantId = null;
        dbContext.Tenants.RemoveRange(dbContext.Tenants);
        dbContext.TenantPackages.RemoveRange(dbContext.TenantPackages);
        await dbContext.SaveChangesAsync();

        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
        await initializer.InitializeAsync();

        var demoTenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Code == "demo");
        var restoredDemoUser = await dbContext.Users.SingleAsync(x => x.UserName == "demo");

        Assert.NotNull(demoTenant);
        Assert.Equal(demoTenantId, demoTenant.Id);
        Assert.Equal(demoTenantId, restoredDemoUser.TenantId);
    }

    [Fact]
    public async Task Protected_Endpoints_Return_Unauthorized_Without_Token()
    {
        var userInfo = await _client.GetAsync("/user/info");
        var accessCodes = await _client.GetAsync("/auth/codes");
        var menus = await _client.GetAsync("/menu/all");

        Assert.Equal(HttpStatusCode.Unauthorized, userInfo.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, accessCodes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, menus.StatusCode);
    }

    [Fact]
    public void Cache_Uses_Distributed_Memory_Cache_By_Default()
    {
        var cache = _factory.Services.GetService<IDistributedCache>();

        Assert.NotNull(cache);
        Assert.Equal(
            "Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache",
            cache.GetType().FullName);
    }

    [Fact]
    public async Task UserInfo_Returns_Roles_And_RealName_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<UserInfoData>>("/user/info");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.False(string.IsNullOrWhiteSpace(json.Data.UserId));
        Assert.Equal("admin", json.Data.Username);
        Assert.Equal("Admin", json.Data.RealName);
        Assert.Contains("admin", json.Data.Roles);
        Assert.Equal("总部", json.Data.DepartmentName);
        Assert.Equal("管理员", json.Data.PositionName);
    }

    [Fact]
    public async Task Current_User_Can_Change_Password_And_Old_Token_Is_Invalidated()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"change-password-{unique}";
        var newPassword = $"Newpass{unique}1";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Change Password User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);

            var changePasswordResponse = await _client.PostAsJsonAsync("/user/change-password", new
            {
                oldPassword = "123456",
                newPassword,
                confirmPassword = newPassword
            });
            changePasswordResponse.EnsureSuccessStatusCode();

            var oldTokenUserInfo = await _client.GetAsync("/user/info");
            Assert.Equal(HttpStatusCode.Unauthorized, oldTokenUserInfo.StatusCode);

            _client.DefaultRequestHeaders.Authorization = null;
            var oldPasswordLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

            var newPasswordLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = newPassword
            });
            newPasswordLogin.EnsureSuccessStatusCode();
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task Current_User_Change_Password_Rejects_Wrong_Old_Password()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"wrong-old-password-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Wrong Old Password User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);

            var changePasswordResponse = await _client.PostAsJsonAsync("/user/change-password", new
            {
                oldPassword = "wrong-password",
                newPassword = $"Newpass{unique}1",
                confirmPassword = $"Newpass{unique}1"
            });

            Assert.Equal(HttpStatusCode.BadRequest, changePasswordResponse.StatusCode);

            _client.DefaultRequestHeaders.Authorization = null;
            var oldPasswordLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            oldPasswordLogin.EnsureSuccessStatusCode();
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task AccessCodes_Returns_Permission_Code_Array_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<string[]>>("/auth/codes");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Contains("system:user:query", json.Data);
        Assert.Contains("system:dashboard:workspace", json.Data);
    }

    [Fact]
    public async Task PermissionDiagnostics_Returns_Effective_User_Authorization()
    {
        await AuthorizeAsync();

        var diagnostics = await _client.GetFromJsonAsync<ApiEnvelope<PermissionDiagnosticsData>>(
            "/system/permission-diagnostics/user/admin");

        Assert.NotNull(diagnostics);
        Assert.Equal("admin", diagnostics.Data.User.UserName);
        Assert.Contains(diagnostics.Data.Roles, role => role.Code == "admin");
        Assert.Contains("system:user:query", diagnostics.Data.PermissionCodes);
        Assert.Contains("system:permission-diagnostics:query", diagnostics.Data.PermissionCodes);
        Assert.Equal("All", diagnostics.Data.DataScope.Level);
        Assert.Contains(diagnostics.Data.MenuItems, menu => menu.Path == "/system/permission-diagnostics");
        Assert.Contains("auth:permissions:admin", diagnostics.Data.Cache.PermissionCodesKey);
        Assert.Contains("auth:menus:admin", diagnostics.Data.Cache.MenusKey);
        Assert.Contains("auth:security-stamp:", diagnostics.Data.Cache.SecurityStampKey);
    }

    [Fact]
    public async Task PermissionDiagnostics_Returns_Tenant_Package_And_Role_Menu_Chain()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var tenantCode = $"diag-{unique}";
        var tenantAdminUserName = $"tenant-diag-{unique}";
        var tenantAdminPassword = $"Tenant{unique}1";

        var createTenantResponse = await _client.PostAsJsonAsync("/platform/tenant", new
        {
            code = tenantCode,
            name = "诊断链路租户",
            adminUserName = tenantAdminUserName,
            adminRealName = "诊断链路管理员",
            adminPassword = tenantAdminPassword
        });
        createTenantResponse.EnsureSuccessStatusCode();

        var diagnostics = await _client.GetFromJsonAsync<ApiEnvelope<PermissionDiagnosticsData>>(
            $"/system/permission-diagnostics/user/{tenantAdminUserName}");

        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.Data.Tenant.IsTenant);
        Assert.Equal(tenantCode, diagnostics.Data.Tenant.TenantCode);
        Assert.Equal("默认套餐", diagnostics.Data.Tenant.PackageName);
        Assert.True(diagnostics.Data.Tenant.PackageMenuCount > 0);
        Assert.True(diagnostics.Data.Effective.RoleMenuCount > 0);
        Assert.True(diagnostics.Data.Effective.FinalMenuCount > 0);
        Assert.Contains(diagnostics.Data.Roles, role =>
            role.Code == "tenant-admin" && role.MenuCount > 0 && role.VisibleMenuCount > 0);
        Assert.DoesNotContain(diagnostics.Data.Warnings, warning => warning.Code == "NoRoleMenus");
    }

    [Fact]
    public async Task PermissionDiagnostics_Can_Refresh_User_Authorization_Cache()
    {
        await AuthorizeAsync();

        var response = await _client.PostAsync(
            "/system/permission-diagnostics/user/admin/refresh-cache",
            null);

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(result);
        Assert.True(result.Data);
    }

    [Fact]
    public async Task PermissionDiagnostics_Returns_DataScope_Department_Names_For_Mixed_Scope()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var customRoleCode = $"diag-custom-{unique}";
        var departmentRoleCode = $"diag-department-{unique}";
        var userName = $"diag-mixed-user-{unique}";

        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createDepartmentResponse = await _client.PostAsJsonAsync("/system/department", new
        {
            code = $"diag-custom-dept-{unique}",
            name = "诊断自定义部门",
            parentId = headquarters.Id,
            leader = "",
            phone = "",
            order = 99,
            isEnabled = true
        });
        createDepartmentResponse.EnsureSuccessStatusCode();
        var createdDepartment =
            await createDepartmentResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();
        Assert.NotNull(createdDepartment);

        var customRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = customRoleCode,
            name = "诊断自定义角色",
            dataScope = "custom",
            customDepartmentIds = new[] { Guid.Parse(createdDepartment.Data.Id) },
            isEnabled = true
        });
        Assert.True(
            customRoleResponse.IsSuccessStatusCode,
            await customRoleResponse.Content.ReadAsStringAsync());
        var customRole = await customRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var departmentRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = departmentRoleCode,
            name = "诊断本部门角色",
            dataScope = "department",
            isEnabled = true
        });
        departmentRoleResponse.EnsureSuccessStatusCode();
        var departmentRole =
            await departmentRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        Assert.NotNull(customRole);
        Assert.NotNull(departmentRole);

        UserListItemData? createdUser = null;
        try
        {
            var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
            {
                userName,
                realName = "诊断组合范围用户",
                password = "123456",
                departmentId = research.Id,
                positionId = manager.Id,
                roleIds = new[] { customRole.Data.Id, departmentRole.Data.Id },
                isEnabled = true
            });
            createUserResponse.EnsureSuccessStatusCode();
            var userEnvelope =
                await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
            Assert.NotNull(userEnvelope);
            createdUser = userEnvelope.Data;

            var diagnostics = await _client.GetFromJsonAsync<ApiEnvelope<PermissionDiagnosticsData>>(
                $"/system/permission-diagnostics/user/{userName}");

            Assert.NotNull(diagnostics);
            Assert.Equal("Mixed", diagnostics.Data.DataScope.Level);
            Assert.Equal("组合范围", diagnostics.Data.DataScope.Description);
            Assert.Contains(research.Name, diagnostics.Data.DataScope.DepartmentNames);
            Assert.Contains(createdDepartment.Data.Name, diagnostics.Data.DataScope.DepartmentNames);
            Assert.Contains(
                diagnostics.Data.Roles,
                role => role.Code == customRoleCode &&
                        role.CustomDepartmentIds!.Contains(createdDepartment.Data.Id));
        }
        finally
        {
            await AuthorizeAsync();

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{departmentRole.Data.Id}");
            await _client.DeleteAsync($"/system/role/{customRole.Data.Id}");
            await _client.DeleteAsync($"/system/department/{createdDepartment.Data.Id}");
        }
    }

    [Fact]
    public async Task MenuAll_Returns_Backend_Menu_Routes_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Contains(json.Data, menu => menu.Name == "Dashboard");
        Assert.Contains(json.Data[0].Children, menu => menu.Name == "Workspace");
    }

    [Fact]
    public async Task MenuAll_Returns_System_User_Menu_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        var systemMenu = Assert.Single(json.Data, menu => menu.Name == "System");
        Assert.Contains(systemMenu.Children, menu => menu.Name == "UserManagement");
    }

    [Fact]
    public async Task MenuAll_Returns_Common_System_Management_Menus_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        var systemMenu = Assert.Single(json.Data, menu => menu.Name == "System");
        var childNames = systemMenu.Children.Select(menu => menu.Name).ToArray();

        Assert.Equal(
            [
                "TenantPackage",
                "UserManagement",
                "FileManagement",
                "RoleManagement",
                "MenuManagement",
                "DepartmentManagement",
                "PositionManagement",
                "DevelopmentTools",
                "DictionaryManagement",
                "ParameterSetting",
                "NoticeAnnouncement"
            ],
            childNames);
    }

    [Fact]
    public async Task MenuAll_Returns_TenantPackage_Menu_For_Admin()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        var systemMenu = Assert.Single(json.Data, menu => menu.Name == "System");
        Assert.Contains(systemMenu.Children, menu => menu.Name == "TenantPackage");
    }

    [Fact]
    public async Task DatabaseInitializer_Removes_Legacy_SystemTenantManagement_Menu()
    {
        const string cleanupSeedVersion = "202605290004-legacy-system-tenant-management-cleanup";
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var legacyMenu = await dbContext.Menus.SingleAsync(
            menu => menu.Name == "TenantManagement" && menu.Path == "/system/tenant");

        dbContext.DataSeedVersions.RemoveRange(
            dbContext.DataSeedVersions.Where(version => version.Version == cleanupSeedVersion));
        legacyMenu.IsEnabled = true;
        legacyMenu.IsVisible = true;
        if (!await dbContext.RoleMenus.AnyAsync(
                roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == legacyMenu.Id))
        {
            dbContext.RoleMenus.Add(new RoleMenu
            {
                RoleId = adminRole.Id,
                MenuId = legacyMenu.Id
            });
        }

        await dbContext.SaveChangesAsync();

        await initializer.InitializeAsync();

        await dbContext.Entry(legacyMenu).ReloadAsync();
        var stillAssigned = await dbContext.RoleMenus.AnyAsync(
            roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == legacyMenu.Id);

        Assert.False(legacyMenu.IsEnabled);
        Assert.False(legacyMenu.IsVisible);
        Assert.False(stillAssigned);
    }

    [Fact]
    public async Task MenuAll_Returns_Log_And_Monitor_Menu_Groups_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        var logManagement = Assert.Single(json.Data, menu => menu.Name == "LogManagement");
        Assert.Equal("/log", logManagement.Path);
        Assert.Equal(["OperationLog", "LoginLog"], logManagement.Children.Select(menu => menu.Name).ToArray());
        Assert.Contains(logManagement.Children, menu => menu is { Name: "OperationLog", Path: "/system/log" });
        Assert.Contains(logManagement.Children, menu => menu is { Name: "LoginLog", Path: "/system/login-log" });

        var systemMonitor = Assert.Single(json.Data, menu => menu.Name == "SystemMonitor");
        Assert.Equal("/monitor", systemMonitor.Path);
        Assert.Equal(
            ["SystemMonitorDashboard", "AlertCenter", "AlertRule", "NotificationCenter", "SecurityCenter", "OnlineUser", "ScheduledJob", "PermissionDiagnostics"],
            systemMonitor.Children.Select(menu => menu.Name).ToArray());
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "SystemMonitorDashboard", Path: "/system/monitor" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "AlertCenter", Path: "/system/alert" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "AlertRule", Path: "/system/alert-rule" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "NotificationCenter", Path: "/system/notification" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "SecurityCenter", Path: "/system/security-center" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "OnlineUser", Path: "/system/online-user" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "ScheduledJob", Path: "/system/scheduled-job" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "PermissionDiagnostics", Path: "/system/permission-diagnostics" });
    }

    [Fact]
    public async Task SystemMonitorOverview_Returns_Runtime_Dependencies_And_Recent_Status()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<SystemMonitorOverviewData>>(
            "/system/monitor/overview");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal("Healthy", json.Data.Api.Status);
        Assert.False(string.IsNullOrWhiteSpace(json.Data.Application.Environment));
        Assert.False(string.IsNullOrWhiteSpace(json.Data.Application.RuntimeVersion));
        Assert.True(json.Data.Application.UptimeSeconds >= 0);
        Assert.True(json.Data.Cpu.ProcessorCount >= 1);
        Assert.True(json.Data.Memory.WorkingSetBytes > 0);
        Assert.True(json.Data.Memory.TotalPhysicalMemoryBytes > 0);
        Assert.True(json.Data.Memory.AvailablePhysicalMemoryBytes >= 0);
        Assert.True(json.Data.Memory.PhysicalMemoryUsedPercent >= 0);
        Assert.Contains(json.Data.Dependencies, dependency => dependency.Name == "MySQL");
        Assert.Contains(json.Data.Dependencies, dependency => dependency.Name == "Cache");
        Assert.Contains(json.Data.Dependencies, dependency => dependency.Name == "FileStorage");
        Assert.True(json.Data.Recent.OnlineUserCount >= 0);
        Assert.True(json.Data.Recent.AbnormalFileCount >= 0);
    }

    [Fact]
    public async Task SecurityCenterOverview_Returns_Security_Summaries()
    {
        var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "security-overview-missing",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);

        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<SecurityCenterOverviewData>>(
            "/system/security-center/overview");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.True(json.Data.Account.TotalUserCount >= 1);
        Assert.True(json.Data.Account.EnabledUserCount >= 1);
        Assert.True(json.Data.Login.FailedLoginCount24h >= 1);
        Assert.True(json.Data.Login.FailedUserCount24h >= 1);
        Assert.True(json.Data.Session.OnlineUserCount >= 1);
        Assert.NotNull(json.Data.RecentEvents);
    }

    [Fact]
    public async Task SecurityEventList_Returns_Login_Failure_Event()
    {
        var userName = $"security-event-{Guid.NewGuid():N}";
        var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);

        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<SecurityEventData>>>(
            $"/system/security-event/list?page=1&pageSize=20&userName={userName}");

        Assert.NotNull(json);
        Assert.Contains(json.Data.Items, item =>
            item.EventType == "LoginFailed" &&
            item.UserName == userName &&
            item.Level == "Warning");
    }

    [Fact]
    public async Task SecurityPolicy_Prevents_Disabling_Last_Admin()
    {
        await AuthorizeAsync();

        var adminUser = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=10&userName=admin");
        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");

        Assert.NotNull(adminUser);
        Assert.NotNull(roleList);
        var admin = Assert.Single(adminUser.Data.Items, user => user.UserName == "admin");
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");

        var response = await _client.PutAsJsonAsync($"/system/user/{admin.Id}", new
        {
            realName = admin.RealName,
            email = "",
            password = "",
            departmentId = admin.DepartmentId,
            positionId = admin.PositionId,
            roleIds = new[] { adminRole.Id },
            isEnabled = false
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var userInfo = await _client.GetAsync("/user/info");
        userInfo.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Disabling_User_Invalidates_Token_And_Removes_Online_User()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"disable-user-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");
        var createdUser = await CreateTestUserAsync(
            userName,
            "Disable User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);

            await AuthorizeAsync();
            var updateResponse = await _client.PutAsJsonAsync($"/system/user/{createdUser.Id}", new
            {
                realName = createdUser.RealName,
                email = "",
                password = "",
                departmentId = createdUser.DepartmentId,
                positionId = createdUser.PositionId,
                roleIds = new[] { adminRole.Id },
                isEnabled = false
            });
            updateResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);
            var oldTokenUserInfo = await _client.GetAsync("/user/info");
            Assert.Equal(HttpStatusCode.Unauthorized, oldTokenUserInfo.StatusCode);

            await AuthorizeAsync();
            var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=20&userName={userName}");
            Assert.NotNull(onlineUsers);
            Assert.DoesNotContain(onlineUsers.Data.Items, user => user.UserName == userName);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task AlertList_Returns_Page_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertData>>>(
            "/system/alert/list?page=1&pageSize=10");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.NotNull(json.Data.Items);
        Assert.True(json.Data.Total >= 0);
    }

    [Fact]
    public async Task AlertRuleList_Returns_Default_Rules_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertRuleData>>>(
            "/system/alert-rule/list?page=1&pageSize=20");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal(5, json.Data.Total);
        var memoryRule = Assert.Single(json.Data.Items, item => item.Code == "MemoryHigh");
        Assert.Equal("内存使用率过高", memoryRule.Name);
        Assert.Equal("Warning", memoryRule.Level);
        Assert.Equal(85m, memoryRule.Threshold);
        Assert.True(memoryRule.Enabled);
        Assert.True(memoryRule.NotifyEnabled);
        Assert.Contains(json.Data.Items, item => item.Code == "DependencyUnhealthy");
        Assert.Contains(json.Data.Items, item => item.Code == "ScheduledJobFailed");
        Assert.Contains(json.Data.Items, item => item.Code == "AuditFailureHigh");
        Assert.Contains(json.Data.Items, item => item.Code == "AbnormalFileDetected");
    }

    [Fact]
    public async Task NotificationRouting_AlertRules_Default_To_Admin_Role()
    {
        await AuthorizeAsync();

        var rules = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertRuleData>>>(
            "/system/alert-rule/list?page=1&pageSize=20");

        Assert.NotNull(rules);
        Assert.All(rules.Data.Items, rule =>
        {
            Assert.Contains(rule.Recipients, recipient =>
                recipient.RecipientType == "Role" &&
                recipient.RecipientName == "admin");
        });
    }

    [Fact]
    public async Task AlertRuleUpdate_Persists_Threshold_Level_And_Switches()
    {
        await AuthorizeAsync();
        var rule = await GetAlertRuleByCodeAsync("AuditFailureHigh");

        try
        {
            var updateResponse = await _client.PutAsJsonAsync($"/system/alert-rule/{rule.Id}", new
            {
                level = "Critical",
                threshold = 3,
                windowMinutes = 120,
                enabled = false,
                notifyEnabled = false,
                remark = "测试更新告警规则"
            });
            Assert.True(updateResponse.IsSuccessStatusCode, await updateResponse.Content.ReadAsStringAsync());
            var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<AlertRuleData>>();
            Assert.NotNull(updated);
            Assert.Equal("Critical", updated.Data.Level);
            Assert.Equal(3m, updated.Data.Threshold);
            Assert.Equal(120, updated.Data.WindowMinutes);
            Assert.False(updated.Data.Enabled);
            Assert.False(updated.Data.NotifyEnabled);
            Assert.Equal("测试更新告警规则", updated.Data.Remark);
        }
        finally
        {
            await RestoreAlertRuleAsync(rule);
        }
    }

    [Fact]
    public async Task AlertScanJob_Creates_Abnormal_File_Alert_And_Admin_Can_Acknowledge()
    {
        await AuthorizeAsync();
        var fileId = Guid.NewGuid();
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            dbContext.ManagedFiles.Add(new ManagedFile
            {
                Id = fileId,
                OriginalName = $"missing-alert-{fileId:N}.txt",
                StoredName = $"{fileId:N}.txt",
                ContentType = "text/plain",
                Size = 12,
                StorageProvider = "Local",
                StoragePath = $"missing-alert/{fileId:N}.txt",
                Status = "Missing",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var jobs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=20&jobKey=alert-scan");
        Assert.NotNull(jobs);
        var alertScan = Assert.Single(jobs.Data.Items, job => job.JobKey == "alert-scan");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{alertScan.Id}/run", null);
        Assert.True(runResponse.IsSuccessStatusCode, await runResponse.Content.ReadAsStringAsync());
        var runResult = await runResponse.Content.ReadFromJsonAsync<ApiEnvelope<ScheduledJobRunResultData>>();
        Assert.NotNull(runResult);
        Assert.Equal("Warning", runResult.Data.Status);

        var alerts = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertData>>>(
            "/system/alert/list?page=1&pageSize=10&type=AbnormalFileDetected");
        Assert.NotNull(alerts);
        var alert = Assert.Single(alerts.Data.Items, item => item.Source == "ManagedFile");
        Assert.Equal("AbnormalFileDetected", alert.Type);
        Assert.Equal("Warning", alert.Level);
        Assert.Equal("Active", alert.Status);
        Assert.True(alert.TriggerCount >= 1);

        var acknowledgeResponse = await _client.PostAsJsonAsync(
            $"/system/alert/{alert.Id}/acknowledge",
            new { remark = "测试确认异常文件告警" });
        Assert.True(acknowledgeResponse.IsSuccessStatusCode, await acknowledgeResponse.Content.ReadAsStringAsync());
        var acknowledged = await acknowledgeResponse.Content.ReadFromJsonAsync<ApiEnvelope<AlertData>>();
        Assert.NotNull(acknowledged);
        Assert.Equal("Acknowledged", acknowledged.Data.Status);
        Assert.Equal("admin", acknowledged.Data.AcknowledgedBy);
        Assert.Equal("测试确认异常文件告警", acknowledged.Data.AcknowledgeRemark);
    }

    [Fact]
    public async Task AlertScanJob_Does_Not_Create_Alert_When_Rule_Disabled()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();
        var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");

        try
        {
            await UpdateAlertRuleAsync(rule, enabled: false, notifyEnabled: rule.NotifyEnabled);
            await CreateMissingManagedFileAsync();

            await RunAlertScanJobAsync();

            var alerts = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertData>>>(
                "/system/alert/list?page=1&pageSize=10&type=AbnormalFileDetected");
            Assert.NotNull(alerts);
            Assert.DoesNotContain(alerts.Data.Items, item => item.Source == "ManagedFile");
        }
        finally
        {
            await RestoreAlertRuleAsync(rule);
        }
    }

    [Fact]
    public async Task AlertScanJob_Creates_Admin_User_Notification_And_Does_Not_Duplicate()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();

        await CreateMissingManagedFileAsync();
        await RunAlertScanJobAsync();

        var alerts = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertData>>>(
            "/system/alert/list?page=1&pageSize=10&type=AbnormalFileDetected");
        Assert.NotNull(alerts);
        var alert = Assert.Single(alerts.Data.Items, item => item.Source == "ManagedFile");

        var notifications = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(notifications);
        var alertNotification = Assert.Single(
            notifications.Data.Items,
            item => item.SourceType == "Alert" && item.SourceId == alert.Id);
        Assert.False(alertNotification.IsRead);
        Assert.Equal("SystemAlert", alertNotification.Category);
        Assert.Equal("Warning", alertNotification.Level);
        Assert.Equal("/system/alert", alertNotification.Link);
        Assert.Contains("发现异常文件", alertNotification.Title);
        Assert.True(notifications.Data.UnreadCount >= 1);

        await RunAlertScanJobAsync();

        var notificationsAfterSecondScan = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(notificationsAfterSecondScan);
        Assert.Single(
            notificationsAfterSecondScan.Data.Items,
            item => item.SourceType == "Alert" && item.SourceId == alert.Id);
    }

    [Fact]
    public async Task NotificationRouting_AlertScan_Notifies_Selected_User_And_Deduplicates()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();
        var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var demoUser = await dbContext.Users.SingleAsync(user => user.UserName == "demo");

        try
        {
            await UpdateAlertRuleRecipientsAsync(
                rule,
                roleIds: [adminRole.Id.ToString()],
                userIds: [demoUser.Id.ToString()],
                emailEnabled: false);
            await CreateMissingManagedFileAsync();

            await RunAlertScanJobAsync();

            await AuthorizeAsync("demo", "123456", "demo");
            var demoNotifications = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
                "/notification/my?take=20&category=SystemAlert");
            Assert.NotNull(demoNotifications);
            Assert.Single(demoNotifications.Data.Items, item => item.SourceType == "Alert");
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await RestoreAlertRuleAsync(rule);
        }
    }

    [Fact]
    public async Task NotificationRouting_EmailEnabled_Creates_Email_Delivery_Record()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();
        var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var admin = await dbContext.Users.SingleAsync(user => user.UserName == "admin");
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        admin.Email = "admin@example.com";
        await dbContext.SaveChangesAsync();

        try
        {
            await UpdateAlertRuleRecipientsAsync(
                rule,
                roleIds: [adminRole.Id.ToString()],
                userIds: [],
                emailEnabled: true);
            await CreateMissingManagedFileAsync();

            await RunAlertScanJobAsync();

            var deliveries = await dbContext.Set<NotificationDelivery>()
                .Where(item => item.Channel == "Email" && item.UserId == admin.Id)
                .ToArrayAsync();
            Assert.Single(deliveries);
            Assert.Contains(deliveries.Single().Status, new[] { "Pending", "Succeeded", "Failed", "Skipped" });
        }
        finally
        {
            await RestoreAlertRuleAsync(rule);
        }
    }

    [Fact]
    public async Task Current_User_Can_Read_And_Clear_Notifications()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();

        await CreateMissingManagedFileAsync();
        await RunAlertScanJobAsync();

        var notifications = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(notifications);
        var notification = Assert.Single(
            notifications.Data.Items,
            item => item.Title.Contains("发现异常文件", StringComparison.Ordinal));
        Assert.False(notification.IsRead);
        var unreadCount = notifications.Data.UnreadCount;

        var readResponse = await _client.PostAsync($"/notification/{notification.Id}/read", null);
        Assert.True(readResponse.IsSuccessStatusCode, await readResponse.Content.ReadAsStringAsync());

        var afterRead = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(afterRead);
        Assert.True(afterRead.Data.UnreadCount < unreadCount);
        Assert.True(Assert.Single(afterRead.Data.Items, item => item.Id == notification.Id).IsRead);

        var readAllResponse = await _client.PostAsync("/notification/read-all", null);
        Assert.True(readAllResponse.IsSuccessStatusCode, await readAllResponse.Content.ReadAsStringAsync());
        var afterReadAll = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(afterReadAll);
        Assert.Equal(0, afterReadAll.Data.UnreadCount);
        Assert.All(afterReadAll.Data.Items, item => Assert.True(item.IsRead));

        var removeResponse = await _client.DeleteAsync($"/notification/{notification.Id}");
        Assert.True(removeResponse.IsSuccessStatusCode, await removeResponse.Content.ReadAsStringAsync());
        var afterRemoveOne = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(afterRemoveOne);
        Assert.DoesNotContain(afterRemoveOne.Data.Items, item => item.Id == notification.Id);

        var clearResponse = await _client.DeleteAsync("/notification/all");
        Assert.True(clearResponse.IsSuccessStatusCode, await clearResponse.Content.ReadAsStringAsync());

        var afterRemove = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?take=20");
        Assert.NotNull(afterRemove);
        Assert.Empty(afterRemove.Data.Items);
        Assert.Equal(0, afterRemove.Data.Total);
    }

    [Fact]
    public async Task AlertScanJob_Creates_Alert_Without_Notification_When_Rule_Notification_Disabled()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();
        var rule = await GetAlertRuleByCodeAsync("AbnormalFileDetected");

        try
        {
            await UpdateAlertRuleAsync(rule, enabled: true, notifyEnabled: false);
            await CreateMissingManagedFileAsync();

            await RunAlertScanJobAsync();

            var alerts = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertData>>>(
                "/system/alert/list?page=1&pageSize=10&type=AbnormalFileDetected");
            Assert.NotNull(alerts);
            var alert = Assert.Single(alerts.Data.Items, item => item.Source == "ManagedFile");

            var notifications = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
                "/notification/my?take=20&category=SystemAlert");
            Assert.NotNull(notifications);
            Assert.DoesNotContain(notifications.Data.Items, item => item.SourceType == "Alert" && item.SourceId == alert.Id);
        }
        finally
        {
            await RestoreAlertRuleAsync(rule);
        }
    }

    [Fact]
    public async Task Current_User_Notifications_Support_Filter_And_Paging()
    {
        await AuthorizeAsync();
        await ClearAlertsAndNotificationsAsync();
        await SeedAdminNotificationsAsync();

        var unreadSystemAlerts = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?page=1&pageSize=1&isRead=false&category=SystemAlert");
        Assert.NotNull(unreadSystemAlerts);
        Assert.Equal(2, unreadSystemAlerts.Data.Total);
        Assert.Equal(3, unreadSystemAlerts.Data.UnreadCount);
        var firstPageItem = Assert.Single(unreadSystemAlerts.Data.Items);
        Assert.Equal("SystemAlert", firstPageItem.Category);
        Assert.False(firstPageItem.IsRead);
        Assert.Equal("系统告警二", firstPageItem.Title);

        var secondPage = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?page=2&pageSize=1&isRead=false&category=SystemAlert");
        Assert.NotNull(secondPage);
        Assert.Equal(2, secondPage.Data.Total);
        var secondPageItem = Assert.Single(secondPage.Data.Items);
        Assert.Equal("系统告警一", secondPageItem.Title);

        var readItems = await _client.GetFromJsonAsync<ApiEnvelope<UserNotificationListData>>(
            "/notification/my?page=1&pageSize=10&isRead=true");
        Assert.NotNull(readItems);
        var readItem = Assert.Single(readItems.Data.Items);
        Assert.True(readItem.IsRead);
        Assert.Equal("已读通知", readItem.Title);
    }

    [Fact]
    public async Task MenuAll_Returns_ScheduledJob_Under_SystemMonitor_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        var systemMonitor = Assert.Single(json.Data, menu => menu.Name == "SystemMonitor");
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "ScheduledJob", Path: "/system/scheduled-job" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "AlertCenter", Path: "/system/alert" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "AlertRule", Path: "/system/alert-rule" });
        Assert.Contains(systemMonitor.Children, menu => menu is { Name: "NotificationCenter", Path: "/system/notification" });
    }

    [Fact]
    public async Task Login_Rejects_Wrong_Password()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginSecurity_Requires_Captcha_After_Three_Failed_Attempts()
    {
        var userName = $"captcha-required-{Guid.NewGuid():N}";

        for (var index = 0; index < 3; index++)
        {
            var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "wrong-password"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginFailureData>>();

        Assert.NotNull(json);
        Assert.True(json.Data.CaptchaRequired);
        Assert.Null(json.Data.LockRemainingSeconds);
    }

    [Fact]
    public async Task LoginSecurity_Locks_Login_After_Five_Failed_Attempts()
    {
        var userName = $"locked-login-{Guid.NewGuid():N}";

        for (var index = 0; index < 5; index++)
        {
            var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "wrong-password"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "wrong-password"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginFailureData>>();

        Assert.NotNull(json);
        Assert.True(json.Data.CaptchaRequired);
        Assert.True(json.Data.LockRemainingSeconds > 0);
    }

    [Fact]
    public async Task LoginSecurity_Admin_Can_Unlock_Locked_Login()
    {
        var userName = $"unlock-login-{Guid.NewGuid():N}";

        for (var index = 0; index < 5; index++)
        {
            var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "wrong-password"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
        }

        var lockedLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "wrong-password"
        });
        var lockedJson = await lockedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginFailureData>>();
        Assert.NotNull(lockedJson);
        Assert.True(lockedJson.Data.LockRemainingSeconds > 0);

        await AuthorizeAsync();
        var unlockResponse = await _client.PostAsync(
            $"/system/user/{Uri.EscapeDataString(userName)}/unlock-login",
            content: null);
        unlockResponse.EnsureSuccessStatusCode();

        _client.DefaultRequestHeaders.Authorization = null;
        var nextFailedLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "wrong-password"
        });
        var nextJson = await nextFailedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginFailureData>>();

        Assert.Equal(HttpStatusCode.Unauthorized, nextFailedLogin.StatusCode);
        Assert.NotNull(nextJson);
        Assert.False(nextJson.Data.CaptchaRequired);
        Assert.Null(nextJson.Data.LockRemainingSeconds);
    }

    [Fact]
    public async Task SecurityPolicy_Returns_Defaults_And_Allows_Admin_Update()
    {
        await AuthorizeAsync();

        var defaults = await _client.GetFromJsonAsync<ApiEnvelope<SecurityPolicyData>>(
            "/system/security-policy");

        Assert.NotNull(defaults);
        Assert.Equal(3, defaults.Data.CaptchaRequiredFailures);
        Assert.Equal(5, defaults.Data.LockoutFailures);
        Assert.Equal(10, defaults.Data.LockoutMinutes);
        Assert.Equal(120, defaults.Data.CaptchaExpireSeconds);
        Assert.Equal(30, defaults.Data.OnlineActiveTimeoutMinutes);
        Assert.Equal(30, defaults.Data.OnlineTouchThrottleSeconds);
        Assert.Equal(90, defaults.Data.StaleUserDays);

        try
        {
            var updateResponse = await _client.PutAsJsonAsync("/system/security-policy", new
            {
                captchaRequiredFailures = 2,
                lockoutFailures = 4,
                lockoutMinutes = 8,
                captchaExpireSeconds = 180,
                onlineActiveTimeoutMinutes = 45,
                onlineTouchThrottleSeconds = 20,
                staleUserDays = 60
            });
            updateResponse.EnsureSuccessStatusCode();

            var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SecurityPolicyData>>();
            Assert.NotNull(updated);
            Assert.Equal(2, updated.Data.CaptchaRequiredFailures);
            Assert.Equal(4, updated.Data.LockoutFailures);
            Assert.Equal(8, updated.Data.LockoutMinutes);
            Assert.Equal(180, updated.Data.CaptchaExpireSeconds);
            Assert.Equal(45, updated.Data.OnlineActiveTimeoutMinutes);
            Assert.Equal(20, updated.Data.OnlineTouchThrottleSeconds);
            Assert.Equal(60, updated.Data.StaleUserDays);

            var reloaded = await _client.GetFromJsonAsync<ApiEnvelope<SecurityPolicyData>>(
                "/system/security-policy");
            Assert.NotNull(reloaded);
            Assert.Equal(2, reloaded.Data.CaptchaRequiredFailures);
            Assert.Equal(60, reloaded.Data.StaleUserDays);
        }
        finally
        {
            await RestoreDefaultSecurityPolicyAsync();
        }
    }

    [Fact]
    public async Task LoginSecurity_Uses_Updated_Captcha_Threshold_From_SecurityPolicy()
    {
        await AuthorizeAsync();
        try
        {
            var updateResponse = await _client.PutAsJsonAsync("/system/security-policy", new
            {
                captchaRequiredFailures = 1,
                lockoutFailures = 5,
                lockoutMinutes = 10,
                captchaExpireSeconds = 120,
                onlineActiveTimeoutMinutes = 30,
                onlineTouchThrottleSeconds = 30,
                staleUserDays = 90
            });
            updateResponse.EnsureSuccessStatusCode();
            _client.DefaultRequestHeaders.Authorization = null;

            var userName = $"dynamic-captcha-{Guid.NewGuid():N}";
            var firstFailedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "wrong-password"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, firstFailedLogin.StatusCode);

            var response = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "wrong-password"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginFailureData>>();

            Assert.NotNull(json);
            Assert.True(json.Data.CaptchaRequired);
            Assert.Null(json.Data.LockRemainingSeconds);
        }
        finally
        {
            await AuthorizeAsync();
            await RestoreDefaultSecurityPolicyAsync();
        }
    }

    [Fact]
    public async Task UserManagement_Admin_Can_Reset_User_Password()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"reset-password-{unique}";
        var newPassword = $"Resetpass{unique}1";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Reset Password User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var resetPasswordResponse = await _client.PostAsJsonAsync(
                $"/system/user/{createdUser.Id}/reset-password",
                new
                {
                    newPassword,
                    confirmPassword = newPassword
                });
            resetPasswordResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = null;
            var oldPasswordLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

            var newPasswordLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = newPassword
            });
            newPasswordLogin.EnsureSuccessStatusCode();
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task UserManagement_Reset_Password_Requires_Reset_Password_Permission()
    {
        var roleCode = $"user-update-no-reset-{Guid.NewGuid():N}";
        var operatorUserName = $"user-update-no-reset-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "User Update Without Reset Password",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var userUpdatePermission = Assert.Single(userMenu.Children, menu => menu.Name == "UserUpdatePermission");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userMenu.Id, userUpdatePermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var operatorUser = await CreateTestUserAsync(
            operatorUserName,
            "User Update Without Reset Password",
            headquarters.Id,
            manager.Id,
            role.Data.Id);

        try
        {
            var operatorLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = operatorUserName,
                password = "123456"
            });
            operatorLogin.EnsureSuccessStatusCode();
            var operatorJson = await operatorLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(operatorJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                operatorJson.Data.AccessToken);

            var forbiddenReset = await _client.PostAsJsonAsync(
                $"/system/user/{operatorUser.Id}/reset-password",
                new
                {
                    newPassword = $"Resetpass{Guid.NewGuid():N}1",
                    confirmPassword = $"Resetpass{Guid.NewGuid():N}1"
                });

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenReset.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{operatorUser.Id}");
            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemUserList_Shows_Login_Lock_Status()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"lock-status-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Lock Status User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            _client.DefaultRequestHeaders.Authorization = null;
            for (var index = 0; index < 5; index++)
            {
                var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
                {
                    username = userName,
                    password = "wrong-password"
                });
                Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);
            }

            await AuthorizeAsync();
            var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
                $"/system/user/list?page=1&pageSize=10&userName={userName}");

            Assert.NotNull(json);
            var lockedUser = Assert.Single(json.Data.Items);
            Assert.Equal(userName, lockedUser.UserName);
            Assert.True(lockedUser.LoginLockRemainingSeconds > 0);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task UserManagement_Unlock_Login_Requires_Unlock_Permission()
    {
        var roleCode = $"user-update-only-{Guid.NewGuid():N}";
        var updateOnlyUserName = $"user-update-only-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "User Update Only",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var userUpdatePermission = Assert.Single(userMenu.Children, menu => menu.Name == "UserUpdatePermission");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userMenu.Id, userUpdatePermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createdUser = await CreateTestUserAsync(
            updateOnlyUserName,
            "User Update Only",
            headquarters.Id,
            manager.Id,
            role.Data.Id);

        try
        {
            var updateOnlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = updateOnlyUserName,
                password = "123456"
            });
            updateOnlyLogin.EnsureSuccessStatusCode();
            var updateOnlyJson = await updateOnlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(updateOnlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                updateOnlyJson.Data.AccessToken);

            var forbiddenUnlock = await _client.PostAsync(
                $"/system/user/{Uri.EscapeDataString("demo")}/unlock-login",
                content: null);

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenUnlock.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task Login_Writes_Login_Log_And_Online_User()
    {
        var failedLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "wrong-password"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, failedLogin.StatusCode);

        await AuthorizeAsync();

        var loginLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<LoginLogData>>>(
            "/system/login-log/list?page=1&pageSize=20&userName=admin");
        var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
            "/system/online-user/list?page=1&pageSize=20&userName=admin");

        Assert.NotNull(loginLogs);
        Assert.NotNull(onlineUsers);
        Assert.Contains(loginLogs.Data.Items, log => log.UserName == "admin" && log.IsSuccess);
        Assert.Contains(loginLogs.Data.Items, log => log.UserName == "admin" && !log.IsSuccess);
        Assert.Contains(onlineUsers.Data.Items, user => user.UserName == "admin");
    }

    [Fact]
    public async Task ForceLogout_Invalidates_User_Token_And_Removes_Online_User()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"online-user-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Online User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);

            await AuthorizeAsync();
            var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=20&userName={userName}");
            Assert.NotNull(onlineUsers);
            Assert.Contains(onlineUsers.Data.Items, user => user.UserName == userName);

            var forceLogout = await _client.PostAsync(
                $"/system/online-user/{createdUser.Id}/force-logout",
                content: null);
            forceLogout.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);
            var oldTokenUserInfo = await _client.GetAsync("/user/info");
            Assert.Equal(HttpStatusCode.Unauthorized, oldTokenUserInfo.StatusCode);

            await AuthorizeAsync();
            var onlineUsersAfterForceLogout = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=20&userName={userName}");
            Assert.NotNull(onlineUsersAfterForceLogout);
            Assert.DoesNotContain(onlineUsersAfterForceLogout.Data.Items, user => user.UserName == userName);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task OnlineUserSessions_Can_Force_Logout_One_Session_Without_Invalidating_Other_Sessions()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"multi-session-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Multi Session User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Chrome/120.0");
            var firstLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            firstLogin.EnsureSuccessStatusCode();
            var firstJson = await firstLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(firstJson);

            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Edg/120.0");
            var secondLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            secondLogin.EnsureSuccessStatusCode();
            var secondJson = await secondLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(secondJson);

            await AuthorizeAsync();
            var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=20&userName={userName}");
            Assert.NotNull(onlineUsers);
            var sessions = onlineUsers.Data.Items.Where(user => user.UserName == userName).ToArray();
            Assert.Equal(2, sessions.Length);
            Assert.Equal(2, sessions.Select(session => session.SessionId).Distinct().Count());

            var forcedSession = Assert.Single(sessions, session => session.SessionId == firstJson.Data.SessionId);
            var forceSessionLogout = await _client.PostAsync(
                $"/system/online-user/session/{forcedSession.SessionId}/force-logout",
                content: null);
            forceSessionLogout.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                firstJson.Data.AccessToken);
            var firstTokenUserInfo = await _client.GetAsync("/user/info");

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                secondJson.Data.AccessToken);
            var secondTokenUserInfo = await _client.GetAsync("/user/info");

            Assert.Equal(HttpStatusCode.Unauthorized, firstTokenUserInfo.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondTokenUserInfo.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.UserAgent.Clear();
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task OnlineUserSessions_Replaces_Previous_Session_For_Same_Browser()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"same-browser-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Same Browser User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.UserAgent.Clear();
            _client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 Chrome/120.0");

            var firstLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            firstLogin.EnsureSuccessStatusCode();
            var firstJson = await firstLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(firstJson);

            var secondLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            secondLogin.EnsureSuccessStatusCode();
            var secondJson = await secondLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(secondJson);

            await AuthorizeAsync();
            var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=20&userName={userName}");
            Assert.NotNull(onlineUsers);
            var sessions = onlineUsers.Data.Items.Where(user => user.UserName == userName).ToArray();

            var session = Assert.Single(sessions);
            Assert.Equal(secondJson.Data.SessionId, session.SessionId);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                firstJson.Data.AccessToken);
            var firstTokenUserInfo = await _client.GetAsync("/user/info");
            Assert.Equal(HttpStatusCode.Unauthorized, firstTokenUserInfo.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            _client.DefaultRequestHeaders.UserAgent.Clear();
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task OnlineUserList_Does_Not_Return_Stale_Online_Records()
    {
        await AuthorizeAsync();
        var userId = Guid.NewGuid();
        var userName = $"stale-online-{Guid.NewGuid():N}";

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            dbContext.OnlineUsers.Add(new OnlineUser
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                UserName = userName,
                RealName = "Stale Online User",
                LoginAt = DateTimeOffset.UtcNow.AddHours(-3),
                LastActiveAt = DateTimeOffset.UtcNow.AddHours(-3),
                IsOnline = true
            });
            await dbContext.SaveChangesAsync();
        }

        var onlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
            $"/system/online-user/list?page=1&pageSize=20&userName={userName}");

        Assert.NotNull(onlineUsers);
        Assert.DoesNotContain(onlineUsers.Data.Items, user => user.UserName == userName);

        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var staleRecord = await verifyDbContext.OnlineUsers.SingleAsync(user => user.UserId == userId);
        Assert.False(staleRecord.IsOnline);
    }

    [Fact]
    public async Task SystemUserList_Returns_Initialized_Users_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=10");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.True(json.Data.Total >= 3);
        Assert.Contains(json.Data.Items, user => user.UserName == "admin" && user.RealName == "Admin");
        Assert.Contains(json.Data.Items, user => user.UserName == "demo" && user.RealName == "Demo User");
        Assert.Contains(json.Data.Items, user =>
            user.UserName == "admin" &&
            user.DepartmentId is not null &&
            user.DepartmentName == "总部");
    }

    [Fact]
    public async Task SeedUsers_Do_Not_Automatically_Get_Admin_Role_Except_Admin()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=10");

        Assert.NotNull(json);
        Assert.Contains(json.Data.Items, user => user.UserName == "admin" && user.Roles.Contains("admin"));
        Assert.DoesNotContain(json.Data.Items, user => user.UserName == "demo" && user.Roles.Contains("admin"));
        Assert.DoesNotContain(json.Data.Items, user => user.UserName == "auditor" && user.Roles.Contains("admin"));
    }

    [Fact]
    public async Task SystemUserList_Filters_By_UserName()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=10&userName=demo");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal(1, json.Data.Total);
        Assert.Equal("demo", json.Data.Items.Single().UserName);
    }

    [Fact]
    public async Task SystemUserList_Filters_By_Department_And_Position()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"filter-user-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Filter User",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(created);

            var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
                $"/system/user/list?page=1&pageSize=100&departmentId={headquarters.Id}&positionId={manager.Id}");

            Assert.NotNull(json);
            Assert.Equal(0, json.Code);
            Assert.Contains(json.Data.Items, user => user.UserName == userName);
            Assert.All(json.Data.Items, user =>
            {
                Assert.Equal(headquarters.Id, user.DepartmentId);
                Assert.Equal(manager.Id, user.PositionId);
            });
        }
        finally
        {
            if (created is not null)
            {
                await _client.DeleteAsync($"/system/user/{created.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task UserList_Applies_Department_DataScope_From_Current_User_Roles()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var scopedRoleCode = $"dept-scope-{unique}";
        var scopedUserName = $"dept-scope-user-{unique}";
        var sameDepartmentUserName = $"same-dept-user-{unique}";
        var otherDepartmentUserName = $"other-dept-user-{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = scopedRoleCode,
            name = "Department Scoped Role",
            dataScope = "department",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var scopedRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var adminRoleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=developer");

        Assert.NotNull(scopedRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(adminRoleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var userUpdatePermission = Assert.Single(userMenu.Children, menu => menu.Name == "UserUpdatePermission");
        var userDeletePermission = Assert.Single(userMenu.Children, menu => menu.Name == "UserDeletePermission");
        var adminRole = Assert.Single(adminRoleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var developer = Assert.Single(positionList.Data.Items, position => position.Code == "developer");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{scopedRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userMenu.Id, userUpdatePermission.Id, userDeletePermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        UserListItemData? scopedUser = null;
        UserListItemData? sameDepartmentUser = null;
        UserListItemData? otherDepartmentUser = null;
        try
        {
            var createScopedUserResponse = await _client.PostAsJsonAsync("/system/user", new
            {
                userName = scopedUserName,
                realName = "Department Scoped User",
                password = "123456",
                departmentId = research.Id,
                positionId = developer.Id,
                roleIds = new[] { scopedRole.Data.Id },
                isEnabled = true
            });
            createScopedUserResponse.EnsureSuccessStatusCode();
            var scopedUserEnvelope =
                await createScopedUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
            Assert.NotNull(scopedUserEnvelope);
            scopedUser = scopedUserEnvelope.Data;

            var createSameDepartmentUserResponse = await _client.PostAsJsonAsync("/system/user", new
            {
                userName = sameDepartmentUserName,
                realName = "Same Department User",
                password = "123456",
                departmentId = research.Id,
                positionId = developer.Id,
                roleIds = new[] { adminRole.Id },
                isEnabled = true
            });
            createSameDepartmentUserResponse.EnsureSuccessStatusCode();
            var sameDepartmentUserEnvelope =
                await createSameDepartmentUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
            Assert.NotNull(sameDepartmentUserEnvelope);
            sameDepartmentUser = sameDepartmentUserEnvelope.Data;

            var createOtherDepartmentUserResponse = await _client.PostAsJsonAsync("/system/user", new
            {
                userName = otherDepartmentUserName,
                realName = "Other Department User",
                password = "123456",
                departmentId = operations.Id,
                positionId = developer.Id,
                roleIds = new[] { adminRole.Id },
                isEnabled = true
            });
            createOtherDepartmentUserResponse.EnsureSuccessStatusCode();
            var otherDepartmentUserEnvelope =
                await createOtherDepartmentUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
            Assert.NotNull(otherDepartmentUserEnvelope);
            otherDepartmentUser = otherDepartmentUserEnvelope.Data;

            var scopedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = scopedUserName,
                password = "123456"
            });
            scopedLogin.EnsureSuccessStatusCode();
            var scopedJson = await scopedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(scopedJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scopedJson.Data.AccessToken);

            var scopedList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
                "/system/user/list?page=1&pageSize=100");

            Assert.NotNull(scopedList);
            Assert.Contains(scopedList.Data.Items, user => user.UserName == scopedUserName);
            Assert.Contains(scopedList.Data.Items, user => user.UserName == sameDepartmentUserName);
            Assert.DoesNotContain(scopedList.Data.Items, user => user.UserName == otherDepartmentUserName);

            var updateOtherDepartmentResponse = await _client.PutAsJsonAsync(
                $"/system/user/{otherDepartmentUser.Id}",
                new
                {
                    realName = "Updated Other Department User",
                    password = (string?)null,
                    departmentId = operations.Id,
                    positionId = developer.Id,
                    roleIds = new[] { adminRole.Id },
                    isEnabled = true
                });

            Assert.Equal(HttpStatusCode.NotFound, updateOtherDepartmentResponse.StatusCode);

            var deleteOtherDepartmentResponse = await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            var deleteOtherDepartment =
                await deleteOtherDepartmentResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

            Assert.NotNull(deleteOtherDepartment);
            Assert.Equal(HttpStatusCode.Forbidden, deleteOtherDepartmentResponse.StatusCode);
            Assert.Equal(1, deleteOtherDepartment.Code);
            Assert.Equal("没有权限删除其他部门账户.", deleteOtherDepartment.Message);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (otherDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            }

            if (sameDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{sameDepartmentUser.Id}");
            }

            if (scopedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{scopedUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{scopedRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemUserCrud_Creates_Updates_And_Deletes_User_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"temp-user-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Temp User",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        Assert.NotNull(created);
        Assert.Equal(userName, created.Data.UserName);
        Assert.Equal("总部", created.Data.DepartmentName);
        Assert.Equal("管理员", created.Data.PositionName);
        Assert.Contains("admin", created.Data.Roles);

        var updateResponse = await _client.PutAsJsonAsync($"/system/user/{created.Data.Id}", new
        {
            realName = "Updated Temp User",
            password = (string?)null,
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        Assert.NotNull(updated);
        Assert.Equal("Updated Temp User", updated.Data.RealName);
        Assert.Equal(0, updated.Data.Status);

        var deleteResponse = await _client.DeleteAsync($"/system/user/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var listAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            $"/system/user/list?page=1&pageSize=10&userName={userName}");

        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.Data.Total);
    }

    [Fact]
    public async Task UserManagement_Delete_Requires_Delete_Permission()
    {
        var roleCode = $"user-readonly-{Guid.NewGuid():N}";
        var readonlyUserName = $"user-readonly-{Guid.NewGuid():N}";
        var targetUserName = $"delete-target-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "User Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var adminRole = Assert.Single(roleList.Data.Items, item => item.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createReadonlyUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName = readonlyUserName,
            realName = "User Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { role.Data.Id },
            isEnabled = true
        });
        createReadonlyUserResponse.EnsureSuccessStatusCode();
        var readonlyUser = await createReadonlyUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createTargetUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName = targetUserName,
            realName = "Delete Target",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createTargetUserResponse.EnsureSuccessStatusCode();
        var targetUser = await createTargetUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(readonlyUser);
            Assert.NotNull(targetUser);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = readonlyUserName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/user/{targetUser.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (targetUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{targetUser.Data.Id}");
            }

            if (readonlyUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{readonlyUser.Data.Id}");
            }

        await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task UserManagement_Does_Not_Delete_Current_User()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"self-delete-{unique}";
        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Self Delete User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        var userLogin = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = userName,
            password = "123456"
        });
        userLogin.EnsureSuccessStatusCode();
        var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
        Assert.NotNull(userJson);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            userJson.Data.AccessToken);

        try
        {
            var deleteSelfResponse = await _client.DeleteAsync($"/system/user/{createdUser.Id}");
            var deleteSelf = await deleteSelfResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

            Assert.NotNull(deleteSelf);
            Assert.Equal(HttpStatusCode.BadRequest, deleteSelfResponse.StatusCode);
            Assert.Equal("不能删除当前登录账户.", deleteSelf.Message);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task Updating_User_Roles_Invalidates_Previous_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"stamp-user-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createdUser = await CreateTestUserAsync(
            userName,
            "Stamp User",
            headquarters.Id,
            manager.Id,
            adminRole.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);

            await AuthorizeAsync();
            var updateResponse = await _client.PutAsJsonAsync($"/system/user/{createdUser.Id}", new
            {
                realName = "Stamp User",
                password = (string?)null,
                departmentId = headquarters.Id,
                positionId = manager.Id,
                roleIds = Array.Empty<string>(),
                isEnabled = true
            });
            updateResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);
            var previousTokenResponse = await _client.GetAsync("/user/info");

            Assert.Equal(HttpStatusCode.Unauthorized, previousTokenResponse.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
        }
    }

    [Fact]
    public async Task Updating_Role_Menus_Invalidates_Assigned_Users_Previous_Tokens()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var roleCode = $"stamp-role-{unique}";
        var userName = $"stamp-role-user-{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "Stamp Role",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var userQueryPermission = Assert.Single(userMenu.Children, menu => menu.Name == "UserQueryPermission");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userMenu.Id, userQueryPermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createdUser = await CreateTestUserAsync(
            userName,
            "Stamp Role User",
            headquarters.Id,
            manager.Id,
            role.Data.Id);

        try
        {
            var userLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            userLogin.EnsureSuccessStatusCode();
            var userJson = await userLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(userJson);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);
            var allowedList = await _client.GetAsync("/system/user/list?page=1&pageSize=10");
            Assert.Equal(HttpStatusCode.OK, allowedList.StatusCode);

            await AuthorizeAsync();
            var removeMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
            {
                menuIds = Array.Empty<string>()
            });
            removeMenusResponse.EnsureSuccessStatusCode();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                userJson.Data.AccessToken);
            var previousTokenResponse = await _client.GetAsync("/user/info");

            Assert.Equal(HttpStatusCode.Unauthorized, previousTokenResponse.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            await _client.DeleteAsync($"/system/user/{createdUser.Id}");
            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemRoleList_Returns_Initialized_Admin_Role_With_Token()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Contains(json.Data.Items, role => role.Code == "admin" && role.Name == "Administrator");
    }

    [Fact]
    public async Task SystemRoleCrud_Creates_Updates_And_Deletes_Role_With_Token()
    {
        await AuthorizeAsync();
        var roleCode = $"test-role-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "Test Role",
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        Assert.NotNull(created);
        Assert.Equal(0, created.Code);
        Assert.Equal(roleCode, created.Data.Code);
        Assert.Equal(1, created.Data.Status);

        var updateResponse = await _client.PutAsJsonAsync($"/system/role/{created.Data.Id}", new
        {
            name = "Updated Test Role",
            isEnabled = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        Assert.NotNull(updated);
        Assert.Equal("Updated Test Role", updated.Data.Name);
        Assert.Equal(0, updated.Data.Status);

        var deleteResponse = await _client.DeleteAsync($"/system/role/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var listAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            $"/system/role/list?page=1&pageSize=10&code={roleCode}");

        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.Data.Total);
    }

    [Fact]
    public async Task SystemRoleCrud_Does_Not_Delete_Admin_Role()
    {
        await AuthorizeAsync();

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");

        Assert.NotNull(roleList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");

        var deleteResponse = await _client.DeleteAsync($"/system/role/{adminRole.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.False(deleted.Data);

        var roleListAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");

        Assert.NotNull(roleListAfterDelete);
        Assert.Contains(roleListAfterDelete.Data.Items, role => role.Code == "admin");
    }

    [Fact]
    public async Task RoleManagement_Delete_Requires_Delete_Permission()
    {
        var readonlyRoleCode = $"role-readonly-{Guid.NewGuid():N}";
        var targetRoleCode = $"role-delete-target-{Guid.NewGuid():N}";
        var userName = $"role-readonly-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var readonlyRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Role Readonly",
            isEnabled = true
        });
        readonlyRoleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await readonlyRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var targetRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = targetRoleCode,
            name = "Role Delete Target",
            isEnabled = true
        });
        targetRoleResponse.EnsureSuccessStatusCode();
        var targetRole = await targetRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(targetRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var roleMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "RoleManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, roleMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Role Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(createdUser);
            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/role/{targetRole.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{targetRole.Data.Id}");
            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task RoleManagement_Assign_Requires_Assign_Permission()
    {
        var readonlyRoleCode = $"role-readonly-{Guid.NewGuid():N}";
        var targetRoleCode = $"role-assign-target-{Guid.NewGuid():N}";
        var userName = $"role-readonly-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var readonlyRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Role Readonly",
            isEnabled = true
        });
        readonlyRoleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await readonlyRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var targetRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = targetRoleCode,
            name = "Role Assign Target",
            isEnabled = true
        });
        targetRoleResponse.EnsureSuccessStatusCode();
        var targetRole = await targetRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(targetRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var roleMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "RoleManagement");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, roleMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Role Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(createdUser);
            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenAssign = await _client.PutAsJsonAsync($"/system/role/{targetRole.Data.Id}/menus", new
            {
                menuIds = new[] { systemMenu.Id, userMenu.Id }
            });

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenAssign.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{targetRole.Data.Id}");
            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemRoleMenus_Can_Read_And_Update_Menu_Assignments()
    {
        await AuthorizeAsync();
        var roleCode = $"permission-role-{Guid.NewGuid():N}";

        var createResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "Permission Role",
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        Assert.NotNull(created);

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");

        Assert.NotNull(menuTree);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userManagementMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");

        var updateResponse = await _client.PutAsJsonAsync($"/system/role/{created.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, userManagementMenu.Id }
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<string[]>>();

        Assert.NotNull(updated);
        Assert.Contains(systemMenu.Id, updated.Data);
        Assert.Contains(userManagementMenu.Id, updated.Data);

        var assigned = await _client.GetFromJsonAsync<ApiEnvelope<string[]>>(
            $"/system/role/{created.Data.Id}/menus");

        Assert.NotNull(assigned);
        Assert.Equal(
            [systemMenu.Id, userManagementMenu.Id],
            assigned.Data.OrderBy(id => id).ToArray());

        await _client.DeleteAsync($"/system/role/{created.Data.Id}");
    }

    [Fact]
    public async Task RoleMenus_Auto_Include_Ancestors_When_Assigning_Child_Menu()
    {
        await AuthorizeAsync();
        var roleCode = $"route-role-{Guid.NewGuid():N}";
        var userName = $"route-user-{Guid.NewGuid():N}";

        var createRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "Route Role",
            isEnabled = true
        });
        createRoleResponse.EnsureSuccessStatusCode();
        var role = await createRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var userMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "UserManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { userMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();
        var assigned = await assignMenusResponse.Content.ReadFromJsonAsync<ApiEnvelope<string[]>>();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Route User",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { role.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(assigned);
            Assert.Contains(systemMenu.Id, assigned.Data);
            Assert.Contains(userMenu.Id, assigned.Data);
            Assert.NotNull(createdUser);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                var roleId = Guid.Parse(role.Data.Id);
                var systemMenuId = Guid.Parse(systemMenu.Id);
                var parentRoleMenu = await dbContext.RoleMenus.SingleAsync(
                    roleMenu => roleMenu.RoleId == roleId && roleMenu.MenuId == systemMenuId);
                dbContext.RoleMenus.Remove(parentRoleMenu);
                await dbContext.SaveChangesAsync();
            }

            var routeLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            routeLogin.EnsureSuccessStatusCode();
            var routeJson = await routeLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(routeJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                routeJson.Data.AccessToken);

            var menus = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

            Assert.NotNull(menus);
            var visibleSystemMenu = Assert.Single(menus.Data, menu => menu.Name == "System");
            Assert.Contains(visibleSystemMenu.Children, menu => menu.Name == "UserManagement");
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemMenuCrud_Creates_Updates_And_Deletes_Menu_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var menuName = $"TempMenu{unique}";

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuManagementItemData[]>>("/system/menu/list");

        Assert.NotNull(menuTree);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");

        var createResponse = await _client.PostAsJsonAsync("/system/menu", new
        {
            parentId = systemMenu.Id,
            name = menuName,
            path = $"/system/temp-{unique}",
            component = "/system/menu/index",
            redirect = (string?)null,
            title = "临时菜单",
            icon = "lucide:test-tube",
            order = 99,
            affixTab = false,
            permissionCode = $"system:temp-{unique}:query",
            isEnabled = true,
            isVisible = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<MenuManagementItemData>>();

        Assert.NotNull(created);
        Assert.Equal(menuName, created.Data.Name);
        Assert.Equal("临时菜单", created.Data.Title);

        var updateResponse = await _client.PutAsJsonAsync($"/system/menu/{created.Data.Id}", new
        {
            parentId = systemMenu.Id,
            name = menuName,
            path = $"/system/temp-{unique}",
            component = "/system/menu/index",
            redirect = (string?)null,
            title = "已更新临时菜单",
            icon = "lucide:test-tube",
            order = 98,
            affixTab = false,
            permissionCode = $"system:temp-{unique}:query",
            isEnabled = true,
            isVisible = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<MenuManagementItemData>>();

        Assert.NotNull(updated);
        Assert.Equal("已更新临时菜单", updated.Data.Title);
        Assert.False(updated.Data.IsVisible);

        var deleteResponse = await _client.DeleteAsync($"/system/menu/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var menuTreeAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<MenuManagementItemData[]>>("/system/menu/list");

        Assert.NotNull(menuTreeAfterDelete);
        Assert.DoesNotContain(
            menuTreeAfterDelete.Data.SelectMany(menu => menu.Children),
            menu => menu.Name == menuName);
    }

    [Fact]
    public async Task MenuManagement_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"menu-readonly-{Guid.NewGuid():N}";
        var userName = $"menu-readonly-{Guid.NewGuid():N}";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var menuName = $"DeleteTargetMenu{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Menu Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var menuManagement = Assert.Single(systemMenu.Children, menu => menu.Name == "MenuManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, menuManagement.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Menu Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createMenuResponse = await _client.PostAsJsonAsync("/system/menu", new
        {
            parentId = systemMenu.Id,
            name = menuName,
            path = $"/system/delete-target-{unique}",
            component = "/system/menu/index",
            redirect = (string?)null,
            title = "删除权限目标菜单",
            icon = "lucide:test-tube",
            order = 99,
            affixTab = false,
            permissionCode = $"system:delete-target-{unique}:query",
            isEnabled = true,
            isVisible = true
        });
        createMenuResponse.EnsureSuccessStatusCode();
        var createdMenu = await createMenuResponse.Content.ReadFromJsonAsync<ApiEnvelope<MenuManagementItemData>>();

        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdMenu);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/menu/{createdMenu.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdMenu is not null)
            {
                await _client.DeleteAsync($"/system/menu/{createdMenu.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemDepartmentCrud_Creates_Updates_And_Deletes_Department_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var departmentCode = $"temp-{unique}";

        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");

        Assert.NotNull(departmentTree);
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");

        var createResponse = await _client.PostAsJsonAsync("/system/department", new
        {
            parentId = headquarters.Id,
            code = departmentCode,
            name = "临时部门",
            leader = "Test Leader",
            phone = "13800000000",
            order = 99,
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();

        Assert.NotNull(created);
        Assert.Equal(departmentCode, created.Data.Code);
        Assert.Equal("临时部门", created.Data.Name);

        var updateResponse = await _client.PutAsJsonAsync($"/system/department/{created.Data.Id}", new
        {
            parentId = headquarters.Id,
            code = departmentCode,
            name = "已更新临时部门",
            leader = "Updated Leader",
            phone = "13900000000",
            order = 98,
            isEnabled = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();

        Assert.NotNull(updated);
        Assert.Equal("已更新临时部门", updated.Data.Name);
        Assert.False(updated.Data.IsEnabled);

        var deleteResponse = await _client.DeleteAsync($"/system/department/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var departmentTreeAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");

        Assert.NotNull(departmentTreeAfterDelete);
        Assert.DoesNotContain(
            departmentTreeAfterDelete.Data.SelectMany(department => department.Children),
            department => department.Code == departmentCode);
    }

    [Fact]
    public async Task DepartmentManagement_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"department-readonly-{Guid.NewGuid():N}";
        var userName = $"department-readonly-{Guid.NewGuid():N}";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var departmentCode = $"delete-target-{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Department Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var departmentMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "DepartmentManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, departmentMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Department Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createDepartmentResponse = await _client.PostAsJsonAsync("/system/department", new
        {
            parentId = headquarters.Id,
            code = departmentCode,
            name = "删除权限目标部门",
            leader = "Tester",
            phone = "10086",
            order = 99,
            isEnabled = true
        });
        createDepartmentResponse.EnsureSuccessStatusCode();
        var createdDepartment = await createDepartmentResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();

        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdDepartment);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/department/{createdDepartment.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdDepartment is not null)
            {
                await _client.DeleteAsync($"/system/department/{createdDepartment.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemDictionaryCrud_Creates_Updates_And_Deletes_Type_And_Item_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var typeCode = $"temp_dict_{unique}";

        var dictionaryList = await _client.GetFromJsonAsync<ApiEnvelope<DictionaryTypeData[]>>(
            "/system/dictionary/list");

        Assert.NotNull(dictionaryList);
        Assert.Contains(dictionaryList.Data, dictionary => dictionary.Code == "user_status");

        var createTypeResponse = await _client.PostAsJsonAsync("/system/dictionary/type", new
        {
            code = typeCode,
            name = "临时字典",
            order = 99,
            isEnabled = true
        });
        createTypeResponse.EnsureSuccessStatusCode();
        var createdType = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<DictionaryTypeData>>();

        Assert.NotNull(createdType);
        Assert.Equal(typeCode, createdType.Data.Code);

        var createItemResponse = await _client.PostAsJsonAsync("/system/dictionary/item", new
        {
            typeId = createdType.Data.Id,
            label = "临时选项",
            value = "temp",
            color = "blue",
            order = 1,
            isEnabled = true
        });
        createItemResponse.EnsureSuccessStatusCode();
        var createdItem = await createItemResponse.Content.ReadFromJsonAsync<ApiEnvelope<DictionaryItemData>>();

        Assert.NotNull(createdItem);
        Assert.Equal("临时选项", createdItem.Data.Label);

        var updateItemResponse = await _client.PutAsJsonAsync($"/system/dictionary/item/{createdItem.Data.Id}", new
        {
            typeId = createdType.Data.Id,
            label = "已更新选项",
            value = "temp",
            color = "green",
            order = 2,
            isEnabled = false
        });
        updateItemResponse.EnsureSuccessStatusCode();
        var updatedItem = await updateItemResponse.Content.ReadFromJsonAsync<ApiEnvelope<DictionaryItemData>>();

        Assert.NotNull(updatedItem);
        Assert.Equal("已更新选项", updatedItem.Data.Label);
        Assert.False(updatedItem.Data.IsEnabled);

        var deleteItemResponse = await _client.DeleteAsync($"/system/dictionary/item/{createdItem.Data.Id}");
        deleteItemResponse.EnsureSuccessStatusCode();
        var deletedItem = await deleteItemResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deletedItem);
        Assert.True(deletedItem.Data);

        var deleteTypeResponse = await _client.DeleteAsync($"/system/dictionary/type/{createdType.Data.Id}");
        deleteTypeResponse.EnsureSuccessStatusCode();
        var deletedType = await deleteTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deletedType);
        Assert.True(deletedType.Data);
    }

    [Fact]
    public async Task DictionaryManagement_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"dictionary-readonly-{Guid.NewGuid():N}";
        var userName = $"dictionary-readonly-{Guid.NewGuid():N}";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var typeCode = $"delete_dict_{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Dictionary Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var dictionaryMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "DictionaryManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, dictionaryMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Dictionary Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createTypeResponse = await _client.PostAsJsonAsync("/system/dictionary/type", new
        {
            code = typeCode,
            name = "删除权限目标字典",
            order = 99,
            isEnabled = true
        });
        createTypeResponse.EnsureSuccessStatusCode();
        var createdType = await createTypeResponse.Content.ReadFromJsonAsync<ApiEnvelope<DictionaryTypeData>>();

        DictionaryItemData? createdItem = null;
        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdType);

            var createItemResponse = await _client.PostAsJsonAsync("/system/dictionary/item", new
            {
                typeId = createdType.Data.Id,
                label = "删除权限目标选项",
                value = "delete-target",
                color = "red",
                order = 1,
                isEnabled = true
            });
            createItemResponse.EnsureSuccessStatusCode();
            var itemEnvelope = await createItemResponse.Content.ReadFromJsonAsync<ApiEnvelope<DictionaryItemData>>();
            Assert.NotNull(itemEnvelope);
            createdItem = itemEnvelope.Data;

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/dictionary/item/{createdItem.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdItem is not null)
            {
                await _client.DeleteAsync($"/system/dictionary/item/{createdItem.Id}");
            }

            if (createdType is not null)
            {
                await _client.DeleteAsync($"/system/dictionary/type/{createdType.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemParameterCrud_Creates_Updates_And_Deletes_Parameter_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var parameterKey = $"temp.parameter.{unique}";

        var parameterList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<SystemParameterData>>>(
            "/system/parameter/list?page=1&pageSize=10");

        Assert.NotNull(parameterList);
        Assert.Contains(parameterList.Data.Items, parameter => parameter.Key == "site_name");

        var createResponse = await _client.PostAsJsonAsync("/system/parameter", new
        {
            key = parameterKey,
            name = "临时参数",
            value = "temp",
            group = "test",
            remark = "临时参数备注",
            order = 99,
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<SystemParameterData>>();

        Assert.NotNull(created);
        Assert.Equal(parameterKey, created.Data.Key);
        Assert.Equal("temp", created.Data.Value);

        var updateResponse = await _client.PutAsJsonAsync($"/system/parameter/{created.Data.Id}", new
        {
            key = parameterKey,
            name = "已更新临时参数",
            value = "updated",
            group = "test",
            remark = "已更新备注",
            order = 98,
            isEnabled = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<SystemParameterData>>();

        Assert.NotNull(updated);
        Assert.Equal("updated", updated.Data.Value);
        Assert.False(updated.Data.IsEnabled);

        var deleteResponse = await _client.DeleteAsync($"/system/parameter/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var listAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<SystemParameterData>>>(
            $"/system/parameter/list?page=1&pageSize=10&key={parameterKey}");

        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.Data.Total);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Returns_Files_Permissions_And_NoConflicts()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"PreviewCustomer{unique}";
        var routeSegment = $"preview-customer-{unique}";
        var permissionPrefix = $"business:preview-customer-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(moduleName, "客户", permissionPrefix, routeSegment));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        Assert.Contains($"{permissionPrefix}:query", json.Data.PermissionCodes);
        Assert.Contains(json.Data.Files, file => file.RelativePath == $"src/MiniAdmin.Domain/Entities/{moduleName}.cs");
        Assert.Contains(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue");
        Assert.DoesNotContain(json.Data.Files, file => file.HasConflict);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Includes_Runnable_Backend_Crud_Files()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"RunnableCustomer{unique}";
        var routeSegment = $"runnable-customer-{unique}";
        var permissionPrefix = $"business:runnable-customer-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(moduleName, "客户", permissionPrefix, routeSegment));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        var endpoint = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Api/Generated/{moduleName}Endpoints.cs");
        var configuration = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}EntityTypeConfiguration.cs");
        var menuSeed = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}MenuSeed.cs");

        Assert.Contains($"RequirePermission(\"{permissionPrefix}:query\")", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:create\")", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:update\")", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:delete\")", endpoint.Content);
        Assert.Contains($"entity.ToTable(\"mini_{routeSegment.Replace('-', '_')}\")", configuration.Content);
        Assert.Contains("entity.HasKey(x => x.Id)", configuration.Content);
        Assert.Contains($"{permissionPrefix}:query", menuSeed.Content);
        Assert.Contains($"{permissionPrefix}:delete", menuSeed.Content);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Includes_ImportExport_When_Enabled()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"ImportExportCustomer{unique}";
        var routeSegment = $"import-export-customer-{unique}";
        var permissionPrefix = $"business:import-export-customer-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                moduleName,
                "导入导出客户",
                permissionPrefix,
                routeSegment,
                enableImportExport: true));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        Assert.Contains($"{permissionPrefix}:import", json.Data.PermissionCodes);
        Assert.Contains($"{permissionPrefix}:export", json.Data.PermissionCodes);
        var endpoint = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Api/Generated/{moduleName}Endpoints.cs");
        var frontendApi = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/api/business/{routeSegment}.ts");
        var frontendPage = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue");
        var menuSeed = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}MenuSeed.cs");

        Assert.Contains($"/business/{routeSegment}/export", endpoint.Content);
        Assert.Contains($"/business/{routeSegment}/import-template", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:import\")", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:export\")", endpoint.Content);
        Assert.Contains($"export{moduleName}Api", frontendApi.Content);
        Assert.Contains($"previewImport{moduleName}Api", frontendApi.Content);
        Assert.Contains("导入", frontendPage.Content);
        Assert.Contains("导出", frontendPage.Content);
        Assert.Contains($"{permissionPrefix}:import", menuSeed.Content);
        Assert.Contains($"{permissionPrefix}:export", menuSeed.Content);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Includes_Workflow_Binding_When_Enabled()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"WorkflowCustomer{unique}";
        var routeSegment = $"workflow-customer-{unique}";
        var permissionPrefix = $"business:workflow-customer-{unique}";
        const string workflowBusinessType = "customer_approval";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                moduleName,
                "审批客户",
                permissionPrefix,
                routeSegment,
                enableWorkflow: true,
                workflowBusinessType: workflowBusinessType));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        Assert.Contains($"{permissionPrefix}:submit-workflow", json.Data.PermissionCodes);
        Assert.Contains($"{permissionPrefix}:withdraw-workflow", json.Data.PermissionCodes);

        var entity = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Domain/Entities/{moduleName}.cs");
        var contracts = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Application.Contracts/{moduleName}s/{moduleName}Dtos.cs");
        var appService = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Application/{moduleName}s/{moduleName}AppService.cs");
        var repository = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs");
        var endpoint = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Api/Generated/{moduleName}Endpoints.cs");
        var menuSeed = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}MenuSeed.cs");
        var stateHandler = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}WorkflowStateHandler.cs");
        var frontendApi = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/api/business/{routeSegment}.ts");
        var frontendPage = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue");

        Assert.Contains("public string? WorkflowInstanceId { get; set; }", entity.Content);
        Assert.Contains("public string ApprovalStatus { get; set; } = \"Draft\";", entity.Content);
        Assert.Contains("string? WorkflowInstanceId", contracts.Content);
        Assert.Contains("string ApprovalStatus", contracts.Content);
        Assert.Contains($"Submit{moduleName}WorkflowRequest", contracts.Content);
        Assert.Contains($"Withdraw{moduleName}WorkflowRequest", contracts.Content);
        Assert.Contains("IWorkflowAppService workflowAppService", appService.Content);
        Assert.Contains($"ResolveBusinessDefinitionAsync(\"{workflowBusinessType}\"", appService.Content);
        Assert.Contains(
            $"global::MiniAdmin.Domain.Entities.{moduleName}.CreateBusinessKey(id)",
            appService.Content);
        Assert.Contains("StartInstanceAsync", appService.Content);
        Assert.Contains("WithdrawAsync", appService.Content);
        Assert.Contains("SetWorkflowStateAsync", repository.Content);
        Assert.Contains("IWorkflowBusinessStateHandler", stateHandler.Content);
        Assert.Contains("TryParseBusinessKey", stateHandler.Content);
        Assert.Contains("Approved", stateHandler.Content);
        Assert.Contains("Rejected", stateHandler.Content);
        Assert.Contains($"/business/{routeSegment}/{{id:guid}}/submit-workflow", endpoint.Content);
        Assert.Contains($"/business/{routeSegment}/{{id:guid}}/withdraw-workflow", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:submit-workflow\")", endpoint.Content);
        Assert.Contains($"RequirePermission(\"{permissionPrefix}:withdraw-workflow\")", endpoint.Content);
        Assert.Contains($"{permissionPrefix}:submit-workflow", menuSeed.Content);
        Assert.Contains($"{permissionPrefix}:withdraw-workflow", menuSeed.Content);
        Assert.Contains($"submit{moduleName}WorkflowApi", frontendApi.Content);
        Assert.Contains($"withdraw{moduleName}WorkflowApi", frontendApi.Content);
        Assert.Contains("审批状态", frontendPage.Content);
        Assert.Contains("submitWorkflow", frontendPage.Content);
        Assert.Contains("withdrawWorkflow", frontendPage.Content);
        Assert.Contains("workflow_instance_id", json.Data.InstallPlan.CreateTableSql);
        Assert.Contains("approval_status", json.Data.InstallPlan.CreateTableSql);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Qualifies_Workflow_Entity_When_ModulePlural_Equals_ModuleName()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        const string moduleName = "Departments";
        var routeSegment = $"workflow-departments-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                moduleName,
                "审批部门",
                $"business:workflow-departments-{unique}",
                routeSegment,
                enableWorkflow: true,
                workflowBusinessType: $"departments_{unique}"));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        var appService = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Application/{moduleName}/{moduleName}AppService.cs");

        Assert.Contains(
            "global::MiniAdmin.Domain.Entities.Departments.CreateBusinessKey(id)",
            appService.Content);
        Assert.DoesNotContain(
            "                               Departments.CreateBusinessKey(id),",
            appService.Content);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Rejects_Table_Already_Mapped_By_Current_System()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                $"GeneratedDepartment{unique}",
                "重复部门",
                $"business:generated-department-{unique}",
                "departments"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData?>>();

        Assert.NotNull(json);
        Assert.Contains("已被当前系统实体", json.Message);
        Assert.Contains("Department", json.Message);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Rejects_Workflow_When_BusinessType_Missing()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var routeSegment = $"workflow-missing-type-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                $"WorkflowMissingType{unique}",
                "缺少业务类型",
                $"business:workflow-missing-type-{unique}",
                routeSegment,
                enableWorkflow: true,
                workflowBusinessType: " "));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData?>>();

        Assert.NotNull(json);
        Assert.Contains("业务类型", json.Message);
    }

    [Fact]
    public void Persistence_Registers_Workflow_State_Handlers_By_Assembly_Scan()
    {
        var services = new ServiceCollection();
        var registerMethod = typeof(MiniAdminPersistenceServiceCollectionExtensions).GetMethod(
            "RegisterWorkflowBusinessStateHandlers",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(registerMethod);

        registerMethod.Invoke(null, [services, typeof(TestWorkflowBusinessStateHandler).Assembly]);

        var descriptors = services
            .Where(descriptor => descriptor.ServiceType == typeof(IWorkflowBusinessStateHandler))
            .ToArray();

        Assert.Contains(
            descriptors,
            descriptor => descriptor.ImplementationType == typeof(TestWorkflowBusinessStateHandler));
    }

    [Fact]
    public async Task CodeGeneratorPreview_Includes_Tenant_Isolation_For_Tenant_Mode()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"TenantCustomer{unique}";
        var routeSegment = $"tenant-customer-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                moduleName,
                "客户",
                $"business:tenant-customer-{unique}",
                routeSegment,
                "Tenant"));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        var entity = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Domain/Entities/{moduleName}.cs");
        var repository = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs");
        var configuration = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}EntityTypeConfiguration.cs");

        Assert.Contains("public Guid? TenantId { get; set; }", entity.Content);
        Assert.Contains("ICurrentTenant currentTenant", repository.Content);
        Assert.Contains("entity.TenantId = currentTenant.TenantId", repository.Content);
        Assert.Contains("x.TenantId == currentTenant.TenantId", repository.Content);
        Assert.Contains("entity.HasIndex(x => x.TenantId)", configuration.Content);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Includes_DataScope_For_Department_Mode()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"ScopedCustomer{unique}";
        var routeSegment = $"scoped-customer-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateDepartmentScopedCodeGeneratorRequest(
                moduleName,
                "权限客户",
                $"business:scoped-customer-{unique}",
                routeSegment));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        var contracts = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Application.Contracts/{moduleName}s/{moduleName}Dtos.cs");
        var endpoint = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Api/Generated/{moduleName}Endpoints.cs");
        var repository = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs");

        Assert.Contains("string? CurrentUserName = null", contracts.Content);
        Assert.Contains("ClaimsPrincipal user", endpoint.Content);
        Assert.Contains("query with { CurrentUserName = GetRequiredUserName(user) }", endpoint.Content);
        Assert.Contains("IDataScopeProvider dataScopeProvider", repository.Content);
        Assert.Contains("ApplyDataScopeAsync(source, query.CurrentUserName, cancellationToken)", repository.Content);
        Assert.Contains("ApplyDataScopeAsync(ApplyTenantFilter(dbContext.Set<", repository.Content);
        Assert.Contains("dataScope.DepartmentIds.Contains(x.DepartmentId.Value)", repository.Content);
        Assert.Contains("x.DepartmentId == departmentId", repository.Content);
    }

    [Fact]
    public async Task CodeGeneratorPreview_Returns_InstallPlan_With_CreateTableSql_When_Table_Missing()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var routeSegment = $"generated-install-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateCodeGeneratorRequest(
                $"GeneratedInstall{unique}",
                "安装客户",
                $"business:generated-install-{unique}",
                routeSegment,
                "Tenant"));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        Assert.False(json.Data.InstallPlan.TableExists);
        Assert.NotNull(json.Data.InstallPlan.CreateTableSql);
        Assert.Contains("create table", json.Data.InstallPlan.CreateTableSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"mini_{routeSegment.Replace('-', '_')}", json.Data.InstallPlan.CreateTableSql);
        Assert.Contains("TenantId", json.Data.InstallPlan.CreateTableSql);
        Assert.Contains(json.Data.InstallPlan.Steps, step => step.Key == "database-table" && step.Status == "Warning");
    }

    [Fact]
    public async Task CodeGeneratorPreview_Uses_Field_Advanced_Config_In_Generated_Crud()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"AdvancedOrder{unique}";
        var routeSegment = $"advanced-order-{unique}";

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/preview",
            CreateAdvancedCodeGeneratorRequest(
                moduleName,
                "高级订单",
                $"business:advanced-order-{unique}",
                routeSegment));

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorPreviewData>>();

        Assert.NotNull(json);
        var contracts = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Application.Contracts/{moduleName}s/{moduleName}Dtos.cs");
        var repository = Assert.Single(
            json.Data.Files,
            file => file.RelativePath == $"src/MiniAdmin.Infrastructure/Persistence/Ef{moduleName}Repository.cs");
        var configuration = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"src/MiniAdmin.Infrastructure/Persistence/Generated/{moduleName}EntityTypeConfiguration.cs");
        var frontend = Assert.Single(
            json.Data.Files,
            file => file.RelativePath ==
                    $"frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue");

        Assert.Contains("string? OrderName = null", contracts.Content);
        Assert.Contains("string? Status = null", contracts.Content);
        Assert.Contains("DateTimeOffset? PaidAtBegin = null", contracts.Content);
        Assert.Contains("DateTimeOffset? PaidAtEnd = null", contracts.Content);
        Assert.Contains("entity.OrderName.Contains(query.OrderName)", repository.Content);
        Assert.Contains("entity.Status == query.Status", repository.Content);
        Assert.Contains("entity.PaidAt >= query.PaidAtBegin", repository.Content);
        Assert.Contains("entity.PaidAt <= query.PaidAtEnd", repository.Content);
        Assert.Contains("entity.HasIndex(x => x.OrderNo).IsUnique()", configuration.Content);
        Assert.Contains("entity.Property(x => x.OrderName).HasColumnName(\"order_name\").HasMaxLength(80)", configuration.Content);
        Assert.Contains("DatePicker", frontend.Content);
        Assert.Contains("Select", frontend.Content);
        Assert.Contains("Textarea", frontend.Content);
        Assert.Contains("dictionaryCode: 'order_status'", frontend.Content);
    }

    [Fact]
    public async Task CodeGeneratorGenerate_Blocks_Conflicting_Files_By_Default()
    {
        await AuthorizeAsync();

        var response = await _client.PostAsJsonAsync(
            "/system/code-generator/generate",
            new
            {
                overwrite = false,
                preview = CreateCodeGeneratorRequest("Notice", "公告", "business:notice", "notice")
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryData?>>();

        Assert.NotNull(json);
        Assert.Contains("文件已存在", json.Message);
    }

    [Fact]
    public async Task CodeGeneratorGenerate_Records_History_When_Successful()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"GeneratedCustomer{unique}";
        var routeSegment = $"generated-customer-{unique}";

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/system/code-generator/generate",
                new
                {
                    overwrite = false,
                    preview = CreateCodeGeneratorRequest(
                        moduleName,
                        "生成客户",
                        $"business:generated-customer-{unique}",
                        routeSegment)
                });

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryData>>();

            Assert.NotNull(json);
            Assert.Equal("Success", json.Data.Status);
            Assert.Equal(moduleName, json.Data.ModuleName);
            Assert.Contains(json.Data.Files, file => file.RelativePath == $"src/MiniAdmin.Domain/Entities/{moduleName}.cs");

            var history = await _client.GetFromJsonAsync<ApiEnvelope<PageData<CodeGenerationHistoryData>>>(
                $"/system/code-generator/history?page=1&pageSize=10&moduleName={moduleName}");

            Assert.NotNull(history);
            Assert.Contains(history.Data.Items, item => item.ModuleName == moduleName && item.Status == "Success");
        }
        finally
        {
            DeleteGeneratedCodeGeneratorFiles(moduleName, routeSegment);
        }
    }

    [Fact]
    public async Task CodeGeneratorGenerate_AutoInstalls_MenuPermissions_When_Enabled()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"AutoInstallCustomer{unique}";
        var routeSegment = $"auto-install-customer-{unique}";
        var permissionPrefix = $"business:auto-install-customer-{unique}";

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/system/code-generator/generate",
                new
                {
                    overwrite = false,
                    autoInstall = true,
                    preview = CreateCodeGeneratorRequest(
                        moduleName,
                        "自动安装客户",
                        permissionPrefix,
                        routeSegment)
                });

            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryData>>();
            Assert.NotNull(json);

            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var expectedCodes = new[]
            {
                $"{permissionPrefix}:query",
                $"{permissionPrefix}:create",
                $"{permissionPrefix}:update",
                $"{permissionPrefix}:delete"
            };
            var generatedMenus = await dbContext.Menus
                .AsNoTracking()
                .Where(menu => menu.PermissionCode != null && menu.PermissionCode.StartsWith(permissionPrefix))
                .ToArrayAsync();
            var adminRoleId = await dbContext.Roles
                .Where(role => role.Code == "admin")
                .Select(role => role.Id)
                .SingleAsync();
            var generatedMenuIds = generatedMenus.Select(menu => menu.Id).ToArray();
            var adminRoleMenus = await dbContext.RoleMenus
                .AsNoTracking()
                .ToArrayAsync();
            adminRoleMenus = adminRoleMenus
                .Where(roleMenu => roleMenu.RoleId == adminRoleId && generatedMenuIds.Contains(roleMenu.MenuId))
                .ToArray();

            foreach (var permissionCode in expectedCodes)
            {
                Assert.Contains(generatedMenus, menu => menu.PermissionCode == permissionCode);
            }

            Assert.NotEmpty(generatedMenuIds);
            Assert.All(generatedMenuIds, menuId =>
                Assert.Contains(adminRoleMenus, roleMenu => roleMenu.MenuId == menuId));

            var detail = await _client.GetFromJsonAsync<ApiEnvelope<CodeGenerationHistoryDetailData>>(
                $"/system/code-generator/history/{json.Data.Id}");
            Assert.NotNull(detail);
            Assert.Contains(detail.Data.InstallPlan.Steps, step => step.Key == "auto-install" && step.Status == "Done");
            Assert.Contains(detail.Data.InstallPlan.Steps, step => step.Key == "menu-permissions" && step.Status == "Done");
        }
        finally
        {
            await DeleteGeneratedCodeGeneratorDatabaseRowsAsync(permissionPrefix);
            DeleteGeneratedCodeGeneratorFiles(moduleName, routeSegment);
        }
    }

    [Fact]
    public async Task CodeGeneratorGenerate_Does_Not_AutoInstall_MenuPermissions_When_Disabled()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"ManualInstallCustomer{unique}";
        var routeSegment = $"manual-install-customer-{unique}";
        var permissionPrefix = $"business:manual-install-customer-{unique}";

        try
        {
            var response = await _client.PostAsJsonAsync(
                "/system/code-generator/generate",
                new
                {
                    overwrite = false,
                    autoInstall = false,
                    preview = CreateCodeGeneratorRequest(
                        moduleName,
                        "手动安装客户",
                        permissionPrefix,
                        routeSegment)
                });

            response.EnsureSuccessStatusCode();

            await using var scope = _factory.Services.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
            var generatedMenuCount = await dbContext.Menus
                .AsNoTracking()
                .CountAsync(menu => menu.PermissionCode != null && menu.PermissionCode.StartsWith(permissionPrefix));

            Assert.Equal(0, generatedMenuCount);
        }
        finally
        {
            await DeleteGeneratedCodeGeneratorDatabaseRowsAsync(permissionPrefix);
            DeleteGeneratedCodeGeneratorFiles(moduleName, routeSegment);
        }
    }

    [Fact]
    public async Task CodeGeneratorRollback_Removes_GeneratedFiles_MenuPermissions_And_Marks_History()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"RollbackCustomer{unique}";
        var routeSegment = $"rollback-customer-{unique}";
        var permissionPrefix = $"business:rollback-customer-{unique}";
        var entityPath = Path.Combine(
            FindWorkspaceRootForTests(),
            "src",
            "MiniAdmin.Domain",
            "Entities",
            $"{moduleName}.cs");

        try
        {
            var generateResponse = await _client.PostAsJsonAsync(
                "/system/code-generator/generate",
                new
                {
                    overwrite = false,
                    autoInstall = true,
                    preview = CreateCodeGeneratorRequest(
                        moduleName,
                        "回滚客户",
                        permissionPrefix,
                        routeSegment)
                });
            generateResponse.EnsureSuccessStatusCode();
            var generated = await generateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryData>>();
            Assert.NotNull(generated);
            Assert.True(File.Exists(entityPath));

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                Assert.True(await dbContext.Menus.AnyAsync(
                    menu => menu.PermissionCode == $"{permissionPrefix}:query"));
            }

            var rollbackResponse = await _client.PostAsJsonAsync(
                $"/system/code-generator/history/{generated.Data.Id}/rollback",
                new { dropTable = true });

            rollbackResponse.EnsureSuccessStatusCode();
            var rollback = await rollbackResponse.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorRollbackData>>();
            Assert.NotNull(rollback);
            Assert.Equal("RolledBack", rollback.Data.Status);
            Assert.True(rollback.Data.DeletedFileCount > 0);
            Assert.True(rollback.Data.DeletedMenuCount > 0);
            Assert.True(rollback.Data.TableDropSkipped);
            Assert.False(File.Exists(entityPath));

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                Assert.False(await dbContext.Menus.AnyAsync(
                    menu => menu.PermissionCode != null && menu.PermissionCode.StartsWith(permissionPrefix)));
            }

            var detail = await _client.GetFromJsonAsync<ApiEnvelope<CodeGenerationHistoryDetailData>>(
                $"/system/code-generator/history/{generated.Data.Id}");
            Assert.NotNull(detail);
            Assert.Equal("RolledBack", detail.Data.Status);

            var secondRollbackResponse = await _client.PostAsync(
                $"/system/code-generator/history/{generated.Data.Id}/rollback",
                null);
            Assert.Equal(HttpStatusCode.BadRequest, secondRollbackResponse.StatusCode);

            var cleanupTableResponse = await _client.PostAsJsonAsync(
                $"/system/code-generator/history/{generated.Data.Id}/rollback",
                new { dropTable = true });
            cleanupTableResponse.EnsureSuccessStatusCode();
            var cleanupTable = await cleanupTableResponse.Content.ReadFromJsonAsync<ApiEnvelope<CodeGeneratorRollbackData>>();
            Assert.NotNull(cleanupTable);
            Assert.Equal("RolledBack", cleanupTable.Data.Status);
            Assert.Equal(0, cleanupTable.Data.DeletedFileCount);
            Assert.Equal(0, cleanupTable.Data.DeletedMenuCount);
            Assert.True(cleanupTable.Data.TableDropSkipped);
        }
        finally
        {
            await DeleteGeneratedCodeGeneratorDatabaseRowsAsync(permissionPrefix);
            DeleteGeneratedCodeGeneratorFiles(moduleName, routeSegment);
        }
    }

    [Fact]
    public async Task CodeGeneratorHistoryDetail_Returns_Request_Files_And_InstallPlan()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var moduleName = $"HistoryCustomer{unique}";
        var routeSegment = $"history-customer-{unique}";

        try
        {
            var generateResponse = await _client.PostAsJsonAsync(
                "/system/code-generator/generate",
                new
                {
                    overwrite = false,
                    preview = CreateCodeGeneratorRequest(
                        moduleName,
                        "历史客户",
                        $"business:history-customer-{unique}",
                        routeSegment)
                });

            generateResponse.EnsureSuccessStatusCode();
            var generated = await generateResponse.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryData>>();

            Assert.NotNull(generated);

            var detailResponse = await _client.GetAsync($"/system/code-generator/history/{generated.Data.Id}");

            detailResponse.EnsureSuccessStatusCode();
            var detail = await detailResponse.Content.ReadFromJsonAsync<ApiEnvelope<CodeGenerationHistoryDetailData>>();

            Assert.NotNull(detail);
            Assert.Equal(generated.Data.Id, detail.Data.Id);
            Assert.Equal(moduleName, detail.Data.Preview.ModuleName);
            Assert.Equal($"mini_{routeSegment.Replace('-', '_')}", detail.Data.Preview.TableName);
            Assert.Contains(detail.Data.Files, file => file.RelativePath == $"src/MiniAdmin.Domain/Entities/{moduleName}.cs");
            Assert.False(detail.Data.InstallPlan.TableExists);
            Assert.NotNull(detail.Data.InstallPlan.CreateTableSql);
            Assert.Contains("restart", string.Join(' ', detail.Data.InstallPlan.Steps.Select(step => step.Key)));
        }
        finally
        {
            DeleteGeneratedCodeGeneratorFiles(moduleName, routeSegment);
        }
    }

    [Fact]
    public async Task CodeGeneratorHistoryDetail_Returns_NotFound_For_Missing_History()
    {
        await AuthorizeAsync();

        var response = await _client.GetAsync($"/system/code-generator/history/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task DeleteGeneratedCodeGeneratorDatabaseRowsAsync(string permissionPrefix)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var menus = await dbContext.Menus
            .Where(menu => menu.PermissionCode != null && menu.PermissionCode.StartsWith(permissionPrefix))
            .ToArrayAsync();
        if (menus.Length == 0)
        {
            return;
        }

        var menuIds = menus.Select(menu => menu.Id).ToArray();
        var roleMenus = await dbContext.RoleMenus
            .ToArrayAsync();
        roleMenus = roleMenus
            .Where(roleMenu => menuIds.Contains(roleMenu.MenuId))
            .ToArray();
        dbContext.RoleMenus.RemoveRange(roleMenus);
        dbContext.Menus.RemoveRange(menus);
        await dbContext.SaveChangesAsync();
    }

    private static void DeleteGeneratedCodeGeneratorFiles(string moduleName, string routeSegment)
    {
        var modulePlural = moduleName.EndsWith('s') ? moduleName : $"{moduleName}s";
        var root = FindWorkspaceRootForTests();
        var paths = new[]
        {
            Path.Combine(root, "src", "MiniAdmin.Domain", "Entities", $"{moduleName}.cs"),
            Path.Combine(root, "src", "MiniAdmin.Application.Contracts", modulePlural),
            Path.Combine(root, "src", "MiniAdmin.Application", modulePlural),
            Path.Combine(root, "src", "MiniAdmin.Infrastructure", "Persistence", $"Ef{moduleName}Repository.cs"),
            Path.Combine(root, "src", "MiniAdmin.Infrastructure", "Persistence", "Generated", $"{moduleName}EntityTypeConfiguration.cs"),
            Path.Combine(root, "src", "MiniAdmin.Infrastructure", "Persistence", "Generated", $"{moduleName}MenuSeed.cs"),
            Path.Combine(root, "src", "MiniAdmin.Api", "Generated", $"{moduleName}Endpoints.cs"),
            Path.Combine(root, "frontend", "vue-vben-admin", "apps", "web-antd", "src", "api", "business", $"{routeSegment}.ts"),
            Path.Combine(root, "frontend", "vue-vben-admin", "apps", "web-antd", "src", "views", "business", routeSegment)
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                continue;
            }

            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
    }

    private static string FindWorkspaceRootForTests()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "MiniAdmin.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    [Fact]
    public async Task ParameterSetting_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"parameter-readonly-{Guid.NewGuid():N}";
        var userName = $"parameter-readonly-{Guid.NewGuid():N}";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var parameterKey = $"delete.parameter.{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Parameter Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var parameterMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "ParameterSetting");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, parameterMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Parameter Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createParameterResponse = await _client.PostAsJsonAsync("/system/parameter", new
        {
            key = parameterKey,
            name = "删除权限目标参数",
            value = "delete-target",
            group = "test",
            remark = "删除权限测试",
            order = 99,
            isEnabled = true
        });
        createParameterResponse.EnsureSuccessStatusCode();
        var createdParameter =
            await createParameterResponse.Content.ReadFromJsonAsync<ApiEnvelope<SystemParameterData>>();

        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdParameter);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/parameter/{createdParameter.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdParameter is not null)
            {
                await _client.DeleteAsync($"/system/parameter/{createdParameter.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemPositionCrud_Creates_Updates_And_Deletes_Position_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var positionCode = $"temp-position-{unique}";

        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10");

        Assert.NotNull(positionList);
        Assert.Contains(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/position", new
        {
            code = positionCode,
            name = "临时岗位",
            order = 99,
            remark = "临时岗位备注",
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionData>>();

        Assert.NotNull(created);
        Assert.Equal(positionCode, created.Data.Code);
        Assert.Equal("临时岗位", created.Data.Name);

        var updateResponse = await _client.PutAsJsonAsync($"/system/position/{created.Data.Id}", new
        {
            code = positionCode,
            name = "已更新临时岗位",
            order = 98,
            remark = "已更新备注",
            isEnabled = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionData>>();

        Assert.NotNull(updated);
        Assert.Equal("已更新临时岗位", updated.Data.Name);
        Assert.Equal("已更新备注", updated.Data.Remark);
        Assert.False(updated.Data.IsEnabled);

        var deleteResponse = await _client.DeleteAsync($"/system/position/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var listAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            $"/system/position/list?page=1&pageSize=10&code={positionCode}");

        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.Data.Total);
    }

    [Fact]
    public async Task SystemPositionCrud_Does_Not_Delete_Position_Bound_To_User()
    {
        await AuthorizeAsync();

        var userList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            "/system/user/list?page=1&pageSize=10&userName=admin");

        Assert.NotNull(userList);
        var admin = Assert.Single(userList.Data.Items, user => user.UserName == "admin");
        Assert.False(string.IsNullOrWhiteSpace(admin.PositionId));

        var deleteResponse = await _client.DeleteAsync($"/system/position/{admin.PositionId}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.False(deleted.Data);

        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=100");

        Assert.NotNull(positionList);
        Assert.Contains(positionList.Data.Items, position => position.Id == admin.PositionId);
    }

    [Fact]
    public async Task PositionManagement_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"position-readonly-{Guid.NewGuid():N}";
        var userName = $"position-readonly-{Guid.NewGuid():N}";
        var unique = Guid.NewGuid().ToString("N")[..8];
        var positionCode = $"delete-target-position-{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Position Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var positionMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "PositionManagement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, positionMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Position Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createPositionResponse = await _client.PostAsJsonAsync("/system/position", new
        {
            code = positionCode,
            name = "删除权限目标岗位",
            order = 99,
            remark = "rbac delete target",
            isEnabled = true
        });
        createPositionResponse.EnsureSuccessStatusCode();
        var createdPosition = await createPositionResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionData>>();

        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdPosition);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/position/{createdPosition.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdPosition is not null)
            {
                await _client.DeleteAsync($"/system/position/{createdPosition.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task PositionImportExport_Template_Preview_Import_And_Export_Workbooks()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var positionCode = $"import-position-{unique}";

        var templateResponse = await _client.GetAsync("/system/position/import-template");
        Assert.True(
            templateResponse.IsSuccessStatusCode,
            await templateResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            templateResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "mini-admin-position-import-template.xlsx",
            templateResponse.Content.Headers.ContentDisposition?.FileNameStar ??
            templateResponse.Content.Headers.ContentDisposition?.FileName);

        using var previewContent = new MultipartFormDataContent();
        var invalidFile = new ByteArrayContent(CreateUserImportWorkbook(
            [
                ["岗位编码", "岗位名称", "排序", "备注", "启用状态"],
                ["", "缺少编码岗位", "abc", "invalid row", "启用"]
            ]));
        invalidFile.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        previewContent.Add(invalidFile, "file", "positions.xlsx");

        var previewResponse = await _client.PostAsync("/system/position/import/preview", previewContent);
        Assert.True(
            previewResponse.IsSuccessStatusCode,
            await previewResponse.Content.ReadAsStringAsync());
        var preview = await previewResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionImportResultData>>();
        Assert.NotNull(preview);
        Assert.Equal(0, preview.Data.CreatedCount);
        Assert.NotEmpty(preview.Data.Errors);

        using var importContent = new MultipartFormDataContent();
        var validFile = new ByteArrayContent(CreateUserImportWorkbook(
            [
                ["岗位编码", "岗位名称", "排序", "备注", "启用状态"],
                [positionCode, "导入岗位", "99", "imported position", "启用"]
            ]));
        validFile.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        importContent.Add(validFile, "file", "positions.xlsx");

        var importResponse = await _client.PostAsync("/system/position/import", importContent);
        Assert.True(
            importResponse.IsSuccessStatusCode,
            await importResponse.Content.ReadAsStringAsync());
        var importResult = await importResponse.Content.ReadFromJsonAsync<ApiEnvelope<PositionImportResultData>>();
        Assert.NotNull(importResult);
        Assert.Equal(1, importResult.Data.CreatedCount);
        Assert.Empty(importResult.Data.Errors);

        var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            $"/system/position/list?page=1&pageSize=10&code={positionCode}");
        Assert.NotNull(list);
        var imported = Assert.Single(list.Data.Items);
        Assert.Equal("导入岗位", imported.Name);

        var exportResponse = await _client.GetAsync($"/system/position/export?code={positionCode}");
        Assert.True(
            exportResponse.IsSuccessStatusCode,
            await exportResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            exportResponse.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "mini-admin-positions.xlsx",
            exportResponse.Content.Headers.ContentDisposition?.FileNameStar ??
            exportResponse.Content.Headers.ContentDisposition?.FileName);
        var exportBytes = await exportResponse.Content.ReadAsByteArrayAsync();
        Assert.True(exportBytes.Length > 2);
        Assert.Equal((byte)'P', exportBytes[0]);
        Assert.Equal((byte)'K', exportBytes[1]);
    }

    [Fact]
    public async Task SystemNoticeCrud_Creates_Updates_And_Deletes_Notice_With_Token()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var noticeTitle = $"临时公告-{unique}";

        var noticeList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<NoticeData>>>(
            "/system/notice/list?page=1&pageSize=10");

        Assert.NotNull(noticeList);
        Assert.Contains(noticeList.Data.Items, notice => notice.Title == "欢迎使用 MiniAdmin");

        var createResponse = await _client.PostAsJsonAsync("/system/notice", new
        {
            title = noticeTitle,
            type = "notice",
            content = "临时公告内容",
            isPublished = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<NoticeData>>();

        Assert.NotNull(created);
        Assert.Equal(noticeTitle, created.Data.Title);
        Assert.Equal("notice", created.Data.Type);
        Assert.True(created.Data.IsPublished);
        Assert.NotNull(created.Data.PublishedAt);

        var updateResponse = await _client.PutAsJsonAsync($"/system/notice/{created.Data.Id}", new
        {
            title = noticeTitle,
            type = "announcement",
            content = "已更新公告内容",
            isPublished = false
        });
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<ApiEnvelope<NoticeData>>();

        Assert.NotNull(updated);
        Assert.Equal("announcement", updated.Data.Type);
        Assert.Equal("已更新公告内容", updated.Data.Content);
        Assert.False(updated.Data.IsPublished);
        Assert.Null(updated.Data.PublishedAt);

        var deleteResponse = await _client.DeleteAsync($"/system/notice/{created.Data.Id}");
        deleteResponse.EnsureSuccessStatusCode();
        var deleted = await deleteResponse.Content.ReadFromJsonAsync<ApiEnvelope<bool>>();

        Assert.NotNull(deleted);
        Assert.True(deleted.Data);

        var listAfterDelete = await _client.GetFromJsonAsync<ApiEnvelope<PageData<NoticeData>>>(
            $"/system/notice/list?page=1&pageSize=10&title={noticeTitle}");

        Assert.NotNull(listAfterDelete);
        Assert.Equal(0, listAfterDelete.Data.Total);
    }

    [Fact]
    public async Task NoticeAnnouncement_Delete_Requires_Delete_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"notice-readonly-{Guid.NewGuid():N}";
        var userName = $"notice-readonly-{Guid.NewGuid():N}";
        var noticeTitle = $"删除权限目标公告-{Guid.NewGuid():N}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Notice Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var noticeMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "NoticeAnnouncement");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, noticeMenu.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Notice Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        var createNoticeResponse = await _client.PostAsJsonAsync("/system/notice", new
        {
            title = noticeTitle,
            type = "notice",
            content = "删除权限测试公告内容",
            isPublished = true
        });
        createNoticeResponse.EnsureSuccessStatusCode();
        var createdNotice = await createNoticeResponse.Content.ReadFromJsonAsync<ApiEnvelope<NoticeData>>();

        try
        {
            Assert.NotNull(createdUser);
            Assert.NotNull(createdNotice);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/notice/{createdNotice.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdNotice is not null)
            {
                await _client.DeleteAsync($"/system/notice/{createdNotice.Data.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SystemAuditLog_Records_Write_RequestBody_And_Masks_Secrets()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"audit-user-{unique}";
        var password = $"Sensitive-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Audit User",
            password,
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(created);

            var auditLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AuditLogData>>>(
                "/system/audit-log/list?page=1&pageSize=20&userName=admin&method=POST&path=/system/user");

            Assert.NotNull(auditLogs);
            var auditLog = Assert.Single(
                auditLogs.Data.Items,
                log => log.RequestBody.Contains(userName, StringComparison.Ordinal));

            Assert.Equal("admin", auditLog.UserName);
            Assert.Equal("POST", auditLog.Method);
            Assert.Equal("/system/user", auditLog.Path);
            Assert.Equal("System", auditLog.Module);
            Assert.Equal("Create", auditLog.Action);
            Assert.True(auditLog.IsSuccess);
            Assert.Equal(200, auditLog.StatusCode);
            Assert.Contains(userName, auditLog.RequestBody);
            Assert.Contains("\"password\":\"***\"", auditLog.RequestBody);
            Assert.DoesNotContain(password, auditLog.RequestBody);
        }
        finally
        {
            if (created is not null)
            {
                await _client.DeleteAsync($"/system/user/{created.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task SystemAuditLog_Records_Entity_Change_Diff_For_User_Update()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"audit-change-{unique}";
        var password = $"Sensitive-{unique}";
        var originalRealName = "Audit Entity User";
        var updatedRealName = "Audit Entity User Updated";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = originalRealName,
            password,
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(created);

            var updatePath = $"/system/user/{created.Data.Id}";
            var updateResponse = await _client.PutAsJsonAsync(updatePath, new
            {
                realName = updatedRealName,
                password = "",
                departmentId = headquarters.Id,
                positionId = manager.Id,
                roleIds = new[] { adminRole.Id },
                isEnabled = true
            });
            updateResponse.EnsureSuccessStatusCode();

            var auditLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AuditLogData>>>(
                $"/system/audit-log/list?page=1&pageSize=20&userName=admin&method=PUT&path={Uri.EscapeDataString(updatePath)}");

            Assert.NotNull(auditLogs);
            var auditLog = Assert.Single(
                auditLogs.Data.Items,
                log => log.ResourceId == created.Data.Id);
            var change = Assert.Single(auditLog.EntityChanges, item => item.EntityName == "User");

            Assert.Equal(created.Data.Id, change.EntityId);
            Assert.Equal("Update", change.OperationType);
            Assert.Contains(originalRealName, change.BeforeJson);
            Assert.Contains(updatedRealName, change.AfterJson);

            using var diff = JsonDocument.Parse(change.DiffJson);
            var realNameDiff = diff.RootElement.GetProperty("RealName");
            Assert.Equal(originalRealName, realNameDiff.GetProperty("Before").GetString());
            Assert.Equal(updatedRealName, realNameDiff.GetProperty("After").GetString());
        }
        finally
        {
            if (created is not null)
            {
                await _client.DeleteAsync($"/system/user/{created.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task SystemAuditLog_Filters_By_CreatedAt_Range()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"audit-time-{unique}";
        var password = $"Sensitive-{unique}";
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-1);

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Audit Time User",
            password,
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
        var endedAt = DateTimeOffset.UtcNow.AddMinutes(1);

        try
        {
            Assert.NotNull(created);

            var currentRangeLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AuditLogData>>>(
                $"/system/audit-log/list?page=1&pageSize=20&method=POST&path=/system/user&startCreatedAt={Uri.EscapeDataString(startedAt.ToString("O"))}&endCreatedAt={Uri.EscapeDataString(endedAt.ToString("O"))}");
            var futureStart = DateTimeOffset.UtcNow.AddDays(1);
            var futureEnd = DateTimeOffset.UtcNow.AddDays(2);
            var futureRangeLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AuditLogData>>>(
                $"/system/audit-log/list?page=1&pageSize=20&method=POST&path=/system/user&startCreatedAt={Uri.EscapeDataString(futureStart.ToString("O"))}&endCreatedAt={Uri.EscapeDataString(futureEnd.ToString("O"))}");

            Assert.NotNull(currentRangeLogs);
            Assert.NotNull(futureRangeLogs);
            Assert.Contains(
                currentRangeLogs.Data.Items,
                log => log.RequestBody.Contains(userName, StringComparison.Ordinal));
            Assert.Equal(0, futureRangeLogs.Data.Total);
        }
        finally
        {
            if (created is not null)
            {
                await _client.DeleteAsync($"/system/user/{created.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task SystemAuditLog_Exports_Filtered_Csv_With_Masked_RequestBody()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"audit-export-{unique}";
        var password = $"Sensitive-{unique}";

        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var adminRole = Assert.Single(roleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Audit Export User",
            password,
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { adminRole.Id },
            isEnabled = true
        });
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(created);

            var exportResponse = await _client.GetAsync(
                "/system/audit-log/export?userName=admin&method=POST&path=/system/user");
            exportResponse.EnsureSuccessStatusCode();
            var csv = await exportResponse.Content.ReadAsStringAsync();

            Assert.Equal("text/csv", exportResponse.Content.Headers.ContentType?.MediaType);
            Assert.Contains("CreatedAt,UserName,Method,Path,Module,Action,StatusCode,IsSuccess,IpAddress,ElapsedMilliseconds,RequestBody,ErrorMessage", csv);
            Assert.Contains(userName, csv);
            Assert.Contains("\"\"password\"\":\"\"***\"\"", csv);
            Assert.DoesNotContain(password, csv);
        }
        finally
        {
            if (created is not null)
            {
                await _client.DeleteAsync($"/system/user/{created.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task AuditLog_Export_Requires_Export_Permission()
    {
        await AuthorizeAsync();
        var readonlyRoleCode = $"log-readonly-{Guid.NewGuid():N}";
        var userName = $"log-readonly-{Guid.NewGuid():N}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = readonlyRoleCode,
            name = "Log Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var readonlyRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(readonlyRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var logManagement = Assert.Single(menuTree.Data, menu => menu.Name == "LogManagement");
        var operationLog = Assert.Single(logManagement.Children, menu => menu.Name == "OperationLog");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{readonlyRole.Data.Id}/menus", new
        {
            menuIds = new[] { logManagement.Id, operationLog.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "Log Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { readonlyRole.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        try
        {
            Assert.NotNull(createdUser);

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenExport = await _client.GetAsync("/system/audit-log/export");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenExport.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{readonlyRole.Data.Id}");
        }
    }

    [Fact]
    public async Task AuditLogList_Applies_Department_DataScope_From_Current_User_Roles()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var scopedRoleCode = $"log-dept-scope-{unique}";
        var scopedUserName = $"log-scope-user-{unique}";
        var sameDepartmentUserName = $"log-same-user-{unique}";
        var otherDepartmentUserName = $"log-other-user-{unique}";
        var uniquePath = $"/system/audit-scope/{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = scopedRoleCode,
            name = "Log Department Scoped Role",
            dataScope = "department",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var scopedRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var adminRoleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=developer");

        Assert.NotNull(scopedRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(adminRoleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var logManagement = Assert.Single(menuTree.Data, menu => menu.Name == "LogManagement");
        var operationLog = Assert.Single(logManagement.Children, menu => menu.Name == "OperationLog");
        var logExportPermission = Assert.Single(operationLog.Children, menu => menu.Name == "LogExportPermission");
        var adminRole = Assert.Single(adminRoleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var developer = Assert.Single(positionList.Data.Items, position => position.Code == "developer");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{scopedRole.Data.Id}/menus", new
        {
            menuIds = new[] { logManagement.Id, operationLog.Id, logExportPermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        UserListItemData? scopedUser = null;
        UserListItemData? sameDepartmentUser = null;
        UserListItemData? otherDepartmentUser = null;
        var sameDepartmentLogId = Guid.NewGuid();
        var otherDepartmentLogId = Guid.NewGuid();
        try
        {
            scopedUser = await CreateTestUserAsync(
                scopedUserName,
                "Log Scoped User",
                research.Id,
                developer.Id,
                scopedRole.Data.Id);
            sameDepartmentUser = await CreateTestUserAsync(
                sameDepartmentUserName,
                "Log Same Department User",
                research.Id,
                developer.Id,
                adminRole.Id);
            otherDepartmentUser = await CreateTestUserAsync(
                otherDepartmentUserName,
                "Log Other Department User",
                operations.Id,
                developer.Id,
                adminRole.Id);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                dbContext.AuditLogs.AddRange(
                    CreateAuditLog(sameDepartmentLogId, sameDepartmentUserName, uniquePath),
                    CreateAuditLog(otherDepartmentLogId, otherDepartmentUserName, uniquePath));
                await dbContext.SaveChangesAsync();
            }

            var scopedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = scopedUserName,
                password = "123456"
            });
            scopedLogin.EnsureSuccessStatusCode();
            var scopedJson = await scopedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(scopedJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scopedJson.Data.AccessToken);

            var scopedLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AuditLogData>>>(
                $"/system/audit-log/list?page=1&pageSize=100&path={uniquePath}");

            Assert.NotNull(scopedLogs);
            Assert.Contains(scopedLogs.Data.Items, log => log.UserName == sameDepartmentUserName);
            Assert.DoesNotContain(scopedLogs.Data.Items, log => log.UserName == otherDepartmentUserName);

            var exportResponse = await _client.GetAsync(
                $"/system/audit-log/export?path={Uri.EscapeDataString(uniquePath)}");
            exportResponse.EnsureSuccessStatusCode();
            var csv = await exportResponse.Content.ReadAsStringAsync();

            Assert.Contains(sameDepartmentUserName, csv);
            Assert.DoesNotContain(otherDepartmentUserName, csv);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                var testLogs = await dbContext.AuditLogs
                    .Where(log => log.Id == sameDepartmentLogId || log.Id == otherDepartmentLogId)
                    .ToArrayAsync();
                dbContext.AuditLogs.RemoveRange(testLogs);
                await dbContext.SaveChangesAsync();
            }

            await AuthorizeAsync();

            if (otherDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            }

            if (sameDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{sameDepartmentUser.Id}");
            }

            if (scopedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{scopedUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{scopedRole.Data.Id}");
        }
    }

    [Fact]
    public async Task DataScopeProvider_Resolves_Department_And_Children_Scope()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var roleCode = $"scope-provider-{unique}";
        var userName = $"scope-provider-user-{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "Scope Provider Role",
            dataScope = "department-and-children",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        UserListItemData? createdUser = null;
        try
        {
            createdUser = await CreateTestUserAsync(
                userName,
                "Scope Provider User",
                headquarters.Id,
                manager.Id,
                role.Data.Id);

            await using var scope = _factory.Services.CreateAsyncScope();
            var dataScopeProvider = scope.ServiceProvider.GetRequiredService<IDataScopeProvider>();
            var dataScope = await dataScopeProvider.GetAsync(userName);

            Assert.Equal(DataScopeLevel.DepartmentAndChildren, dataScope.Level);
            Assert.Equal(createdUser.Id, dataScope.UserId?.ToString());
            Assert.Equal(userName, dataScope.UserName);
            Assert.Contains(Guid.Parse(headquarters.Id), dataScope.DepartmentIds);
            Assert.Contains(Guid.Parse(research.Id), dataScope.DepartmentIds);
            Assert.Contains(Guid.Parse(operations.Id), dataScope.DepartmentIds);
        }
        finally
        {
            await AuthorizeAsync();

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task RoleCreate_WithCustomScope_Requires_Department_Selection()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];

        var response = await _client.PostAsJsonAsync("/system/role", new
        {
            code = $"custom-empty-{unique}",
            name = "Custom Scope Empty Role",
            dataScope = "custom",
            customDepartmentIds = Array.Empty<string>(),
            isEnabled = true
        });

        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData?>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(json);
        Assert.Equal(1, json.Code);
        Assert.Equal("自定义数据范围至少选择一个部门.", json.Message);
    }

    [Fact]
    public async Task DataScopeProvider_Resolves_Custom_Department_And_Mixed_Scope()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var customRoleCode = $"scope-custom-{unique}";
        var mixedCustomRoleCode = $"scope-mixed-custom-{unique}";
        var mixedDepartmentRoleCode = $"scope-mixed-dept-{unique}";
        var customOnlyUserName = $"scope-custom-user-{unique}";
        var mixedUserName = $"scope-mixed-user-{unique}";

        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var createDepartmentResponse = await _client.PostAsJsonAsync("/system/department", new
        {
            code = $"scope-custom-dept-{unique}",
            name = "Scope Custom Department",
            parentId = headquarters.Id,
            leader = "",
            phone = "",
            order = 99,
            isEnabled = true
        });
        createDepartmentResponse.EnsureSuccessStatusCode();
        var createdDepartment =
            await createDepartmentResponse.Content.ReadFromJsonAsync<ApiEnvelope<DepartmentItemData>>();

        var customRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = customRoleCode,
            name = "Custom Scope Role",
            dataScope = "custom",
            customDepartmentIds = new[] { Guid.Parse(createdDepartment!.Data.Id) },
            isEnabled = true
        });
        Assert.True(
            customRoleResponse.IsSuccessStatusCode,
            await customRoleResponse.Content.ReadAsStringAsync());
        var customRole = await customRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var mixedCustomRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = mixedCustomRoleCode,
            name = "Mixed Custom Scope Role",
            dataScope = "custom",
            customDepartmentIds = new[] { Guid.Parse(createdDepartment!.Data.Id) },
            isEnabled = true
        });
        mixedCustomRoleResponse.EnsureSuccessStatusCode();
        var mixedCustomRole = await mixedCustomRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var mixedDepartmentRoleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = mixedDepartmentRoleCode,
            name = "Mixed Department Scope Role",
            dataScope = "department",
            isEnabled = true
        });
        mixedDepartmentRoleResponse.EnsureSuccessStatusCode();
        var mixedDepartmentRole =
            await mixedDepartmentRoleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        Assert.NotNull(customRole);
        Assert.NotNull(mixedCustomRole);
        Assert.NotNull(mixedDepartmentRole);

        UserListItemData? customOnlyUser = null;
        UserListItemData? mixedUser = null;
        try
        {
            customOnlyUser = await CreateTestUserAsync(
                customOnlyUserName,
                "Custom Scope User",
                research.Id,
                manager.Id,
                customRole.Data.Id);

            var mixedUserResponse = await _client.PostAsJsonAsync("/system/user", new
            {
                userName = mixedUserName,
                realName = "Mixed Scope User",
                password = "123456",
                departmentId = research.Id,
                positionId = manager.Id,
                roleIds = new[] { mixedCustomRole.Data.Id, mixedDepartmentRole.Data.Id },
                isEnabled = true
            });
            mixedUserResponse.EnsureSuccessStatusCode();
            var mixedUserEnvelope =
                await mixedUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
            Assert.NotNull(mixedUserEnvelope);
            mixedUser = mixedUserEnvelope.Data;

            await using var scope = _factory.Services.CreateAsyncScope();
            var dataScopeProvider = scope.ServiceProvider.GetRequiredService<IDataScopeProvider>();

            var customScope = await dataScopeProvider.GetAsync(customOnlyUserName);
            Assert.Equal(DataScopeLevel.CustomDepartments, customScope.Level);
            Assert.Equal(customOnlyUser.Id, customScope.UserId?.ToString());
            Assert.Contains(Guid.Parse(createdDepartment.Data.Id), customScope.DepartmentIds);
            Assert.DoesNotContain(Guid.Parse(research.Id), customScope.DepartmentIds);

            var mixedScope = await dataScopeProvider.GetAsync(mixedUserName);
            Assert.Equal(DataScopeLevel.Mixed, mixedScope.Level);
            Assert.Equal(mixedUser.Id, mixedScope.UserId?.ToString());
            Assert.Contains(Guid.Parse(research.Id), mixedScope.DepartmentIds);
            Assert.Contains(Guid.Parse(createdDepartment.Data.Id), mixedScope.DepartmentIds);
        }
        finally
        {
            await AuthorizeAsync();

            if (mixedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{mixedUser.Id}");
            }

            if (customOnlyUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{customOnlyUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{mixedDepartmentRole.Data.Id}");
            await _client.DeleteAsync($"/system/role/{mixedCustomRole.Data.Id}");
            await _client.DeleteAsync($"/system/role/{customRole.Data.Id}");
            await _client.DeleteAsync($"/system/department/{createdDepartment!.Data.Id}");
        }
    }

    [Fact]
    public async Task LoginLogList_Applies_Department_DataScope_From_Current_User_Roles()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var scopedRoleCode = $"login-log-scope-{unique}";
        var scopedUserName = $"login-log-scope-user-{unique}";
        var sameDepartmentUserName = $"login-log-same-user-{unique}";
        var otherDepartmentUserName = $"login-log-other-user-{unique}";
        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = scopedRoleCode,
            name = "Login Log Scoped Role",
            dataScope = "department",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var scopedRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var adminRoleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=developer");

        Assert.NotNull(scopedRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(adminRoleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var logManagement = Assert.Single(menuTree.Data, menu => menu.Name == "LogManagement");
        var loginLog = Assert.Single(logManagement.Children, menu => menu.Name == "LoginLog");
        var loginLogQueryPermission =
            Assert.Single(loginLog.Children, menu => menu.Name == "LoginLogQueryPermission");
        var adminRole = Assert.Single(adminRoleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var developer = Assert.Single(positionList.Data.Items, position => position.Code == "developer");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{scopedRole.Data.Id}/menus", new
        {
            menuIds = new[] { logManagement.Id, loginLog.Id, loginLogQueryPermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        UserListItemData? scopedUser = null;
        UserListItemData? sameDepartmentUser = null;
        UserListItemData? otherDepartmentUser = null;
        try
        {
            scopedUser = await CreateTestUserAsync(
                scopedUserName,
                "Login Log Scoped User",
                research.Id,
                developer.Id,
                scopedRole.Data.Id);
            sameDepartmentUser = await CreateTestUserAsync(
                sameDepartmentUserName,
                "Login Log Same Department User",
                research.Id,
                developer.Id,
                adminRole.Id);
            otherDepartmentUser = await CreateTestUserAsync(
                otherDepartmentUserName,
                "Login Log Other Department User",
                operations.Id,
                developer.Id,
                adminRole.Id);

            foreach (var userName in new[] { sameDepartmentUserName, otherDepartmentUserName })
            {
                var login = await _client.PostAsJsonAsync("/auth/login", new
                {
                    username = userName,
                    password = "123456"
                });
                login.EnsureSuccessStatusCode();
            }

            var scopedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = scopedUserName,
                password = "123456"
            });
            scopedLogin.EnsureSuccessStatusCode();
            var scopedJson = await scopedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(scopedJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scopedJson.Data.AccessToken);

            var ownLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<LoginLogData>>>(
                $"/system/login-log/list?page=1&pageSize=100&userName={scopedUserName}");
            var sameDepartmentLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<LoginLogData>>>(
                $"/system/login-log/list?page=1&pageSize=100&userName={sameDepartmentUserName}");
            var otherDepartmentLogs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<LoginLogData>>>(
                $"/system/login-log/list?page=1&pageSize=100&userName={otherDepartmentUserName}");

            Assert.NotNull(ownLogs);
            Assert.NotNull(sameDepartmentLogs);
            Assert.NotNull(otherDepartmentLogs);
            Assert.Contains(ownLogs.Data.Items, log => log.UserName == scopedUserName);
            Assert.Contains(sameDepartmentLogs.Data.Items, log => log.UserName == sameDepartmentUserName);
            Assert.DoesNotContain(otherDepartmentLogs.Data.Items, log => log.UserName == otherDepartmentUserName);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (otherDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            }

            if (sameDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{sameDepartmentUser.Id}");
            }

            if (scopedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{scopedUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{scopedRole.Data.Id}");
        }
    }

    [Fact]
    public async Task OnlineUserList_Applies_Department_DataScope_From_Current_User_Roles()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var scopedRoleCode = $"online-scope-{unique}";
        var scopedUserName = $"online-scope-user-{unique}";
        var sameDepartmentUserName = $"online-same-user-{unique}";
        var otherDepartmentUserName = $"online-other-user-{unique}";
        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = scopedRoleCode,
            name = "Online User Scoped Role",
            dataScope = "department",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var scopedRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var adminRoleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=developer");

        Assert.NotNull(scopedRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(adminRoleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMonitor = Assert.Single(menuTree.Data, menu => menu.Name == "SystemMonitor");
        var onlineUser = Assert.Single(systemMonitor.Children, menu => menu.Name == "OnlineUser");
        var onlineUserQueryPermission =
            Assert.Single(onlineUser.Children, menu => menu.Name == "OnlineUserQueryPermission");
        var adminRole = Assert.Single(adminRoleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var developer = Assert.Single(positionList.Data.Items, position => position.Code == "developer");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{scopedRole.Data.Id}/menus", new
        {
            menuIds = new[] { systemMonitor.Id, onlineUser.Id, onlineUserQueryPermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        UserListItemData? scopedUser = null;
        UserListItemData? sameDepartmentUser = null;
        UserListItemData? otherDepartmentUser = null;
        try
        {
            scopedUser = await CreateTestUserAsync(
                scopedUserName,
                "Online Scoped User",
                research.Id,
                developer.Id,
                scopedRole.Data.Id);
            sameDepartmentUser = await CreateTestUserAsync(
                sameDepartmentUserName,
                "Online Same Department User",
                research.Id,
                developer.Id,
                adminRole.Id);
            otherDepartmentUser = await CreateTestUserAsync(
                otherDepartmentUserName,
                "Online Other Department User",
                operations.Id,
                developer.Id,
                adminRole.Id);

            foreach (var userName in new[] { sameDepartmentUserName, otherDepartmentUserName })
            {
                var login = await _client.PostAsJsonAsync("/auth/login", new
                {
                    username = userName,
                    password = "123456"
                });
                login.EnsureSuccessStatusCode();
            }

            var scopedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = scopedUserName,
                password = "123456"
            });
            scopedLogin.EnsureSuccessStatusCode();
            var scopedJson = await scopedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(scopedJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scopedJson.Data.AccessToken);

            var ownOnlineUsers = await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                $"/system/online-user/list?page=1&pageSize=100&userName={scopedUserName}");
            var sameDepartmentOnlineUsers =
                await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                    $"/system/online-user/list?page=1&pageSize=100&userName={sameDepartmentUserName}");
            var otherDepartmentOnlineUsers =
                await _client.GetFromJsonAsync<ApiEnvelope<PageData<OnlineUserData>>>(
                    $"/system/online-user/list?page=1&pageSize=100&userName={otherDepartmentUserName}");

            Assert.NotNull(ownOnlineUsers);
            Assert.NotNull(sameDepartmentOnlineUsers);
            Assert.NotNull(otherDepartmentOnlineUsers);
            Assert.Contains(ownOnlineUsers.Data.Items, user => user.UserName == scopedUserName);
            Assert.Contains(
                sameDepartmentOnlineUsers.Data.Items,
                user => user.UserName == sameDepartmentUserName);
            Assert.DoesNotContain(
                otherDepartmentOnlineUsers.Data.Items,
                user => user.UserName == otherDepartmentUserName);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();

            if (otherDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            }

            if (sameDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{sameDepartmentUser.Id}");
            }

            if (scopedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{scopedUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{scopedRole.Data.Id}");
        }
    }

    [Fact]
    public async Task SecurityEventList_Applies_Department_DataScope_From_Current_User_Roles()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var scopedRoleCode = $"security-scope-{unique}";
        var scopedUserName = $"security-scope-user-{unique}";
        var sameDepartmentUserName = $"security-same-user-{unique}";
        var otherDepartmentUserName = $"security-other-user-{unique}";
        var eventType = $"DepartmentScopeAudit{unique}";

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = scopedRoleCode,
            name = "Security Event Scoped Role",
            dataScope = "department",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var scopedRole = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var adminRoleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=developer");

        Assert.NotNull(scopedRole);
        Assert.NotNull(menuTree);
        Assert.NotNull(adminRoleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMonitor = Assert.Single(menuTree.Data, menu => menu.Name == "SystemMonitor");
        var securityCenter = Assert.Single(systemMonitor.Children, menu => menu.Name == "SecurityCenter");
        var securityCenterQueryPermission =
            Assert.Single(securityCenter.Children, menu => menu.Name == "SecurityCenterQueryPermission");
        var securityEventQueryPermission =
            Assert.Single(securityCenter.Children, menu => menu.Name == "SecurityEventQueryPermission");
        var adminRole = Assert.Single(adminRoleList.Data.Items, role => role.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var research = Assert.Single(headquarters.Children, department => department.Code == "rd");
        var operations = Assert.Single(headquarters.Children, department => department.Code == "ops");
        var developer = Assert.Single(positionList.Data.Items, position => position.Code == "developer");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{scopedRole.Data.Id}/menus", new
        {
            menuIds = new[]
            {
                systemMonitor.Id,
                securityCenter.Id,
                securityCenterQueryPermission.Id,
                securityEventQueryPermission.Id
            }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        UserListItemData? scopedUser = null;
        UserListItemData? sameDepartmentUser = null;
        UserListItemData? otherDepartmentUser = null;
        var sameDepartmentEventId = Guid.NewGuid();
        var otherDepartmentEventId = Guid.NewGuid();
        try
        {
            scopedUser = await CreateTestUserAsync(
                scopedUserName,
                "Security Scoped User",
                research.Id,
                developer.Id,
                scopedRole.Data.Id);
            sameDepartmentUser = await CreateTestUserAsync(
                sameDepartmentUserName,
                "Security Same Department User",
                research.Id,
                developer.Id,
                adminRole.Id);
            otherDepartmentUser = await CreateTestUserAsync(
                otherDepartmentUserName,
                "Security Other Department User",
                operations.Id,
                developer.Id,
                adminRole.Id);

            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                dbContext.SecurityEvents.AddRange(
                    new SecurityEvent
                    {
                        Id = sameDepartmentEventId,
                        EventType = eventType,
                        Level = "Info",
                        UserName = sameDepartmentUserName,
                        Title = "Same Department Event",
                        Description = "Same department security event"
                    },
                    new SecurityEvent
                    {
                        Id = otherDepartmentEventId,
                        EventType = eventType,
                        Level = "Info",
                        UserName = otherDepartmentUserName,
                        Title = "Other Department Event",
                        Description = "Other department security event"
                    });
                await dbContext.SaveChangesAsync();
            }

            var scopedLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = scopedUserName,
                password = "123456"
            });
            scopedLogin.EnsureSuccessStatusCode();
            var scopedJson = await scopedLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(scopedJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                scopedJson.Data.AccessToken);

            var securityEvents = await _client.GetFromJsonAsync<ApiEnvelope<PageData<SecurityEventData>>>(
                $"/system/security-event/list?page=1&pageSize=100&eventType={eventType}");

            Assert.NotNull(securityEvents);
            Assert.Contains(securityEvents.Data.Items, item => item.UserName == sameDepartmentUserName);
            Assert.DoesNotContain(securityEvents.Data.Items, item => item.UserName == otherDepartmentUserName);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
                var testEvents = await dbContext.SecurityEvents
                    .Where(securityEvent =>
                        securityEvent.Id == sameDepartmentEventId ||
                        securityEvent.Id == otherDepartmentEventId)
                    .ToArrayAsync();
                dbContext.SecurityEvents.RemoveRange(testEvents);
                await dbContext.SaveChangesAsync();
            }

            await AuthorizeAsync();

            if (otherDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{otherDepartmentUser.Id}");
            }

            if (sameDepartmentUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{sameDepartmentUser.Id}");
            }

            if (scopedUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{scopedUser.Id}");
            }

            await _client.DeleteAsync($"/system/role/{scopedRole.Data.Id}");
        }
    }

    [Fact]
    public async Task DatabaseInitializer_Removes_AuditLogs_Older_Than_90_Days()
    {
        var oldLogId = Guid.NewGuid();
        var retainedLogId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();

        dbContext.AuditLogs.AddRange(
            CreateAuditLog(oldLogId, DateTimeOffset.UtcNow.AddDays(-91)),
            CreateAuditLog(retainedLogId, DateTimeOffset.UtcNow.AddDays(-89)));
        await dbContext.SaveChangesAsync();

        await initializer.InitializeAsync();

        var remainingIds = await dbContext.AuditLogs
            .Where(log => log.Id == oldLogId || log.Id == retainedLogId)
            .Select(log => log.Id)
            .ToArrayAsync();

        Assert.DoesNotContain(oldLogId, remainingIds);
        Assert.Contains(retainedLogId, remainingIds);
    }

    [Fact]
    public async Task DatabaseInitializer_Does_Not_Regrant_Removed_Admin_Menus()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var tenantMenuIds = await dbContext.Menus
            .Where(menu => menu.Name == "TenantPackage" || menu.Name == "TenantManagement")
            .Select(menu => menu.Id)
            .ToListAsync();
        var tenantRoleMenus = await dbContext.RoleMenus
            .Where(roleMenu => roleMenu.RoleId == adminRole.Id && tenantMenuIds.Contains(roleMenu.MenuId))
            .ToArrayAsync();

        dbContext.RoleMenus.RemoveRange(tenantRoleMenus);
        await dbContext.SaveChangesAsync();

        await initializer.InitializeAsync();

        var restoredTenantMenuCount = await dbContext.RoleMenus
            .CountAsync(roleMenu => roleMenu.RoleId == adminRole.Id && tenantMenuIds.Contains(roleMenu.MenuId));

        Assert.Equal(0, restoredTenantMenuCount);
    }

    [Fact]
    public async Task DatabaseInitializer_Does_Not_Add_RoleMenu_For_Missing_Menu()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var missingMenuId = Guid.NewGuid();

        await InvokePrivateInitializerTaskAsync(
            initializer,
            "EnsureRoleMenuAsync",
            adminRole.Id,
            missingMenuId,
            CancellationToken.None);

        Assert.DoesNotContain(
            dbContext.RoleMenus.Local,
            roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == missingMenuId);
        Assert.False(await dbContext.RoleMenus.AnyAsync(
            roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == missingMenuId));
    }

    [Fact]
    public async Task DatabaseInitializer_Uses_Pending_Parent_RoleMenu_When_Granting_Permission()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var parentMenuId = Guid.NewGuid();
        var permissionMenuId = Guid.NewGuid();

        dbContext.Menus.AddRange(
            new Menu
            {
                Id = parentMenuId,
                Name = "PendingParentRoleMenuTest",
                Path = "/test/pending-parent-role-menu",
                Title = "PendingParentRoleMenuTest",
                PermissionCode = "test:pending-parent-role-menu:query",
                IsEnabled = true
            },
            new Menu
            {
                Id = permissionMenuId,
                ParentId = parentMenuId,
                Name = "PendingParentRoleMenuPermissionTest",
                Path = "test:pending-parent-role-menu:action",
                Title = "test:pending-parent-role-menu:action",
                PermissionCode = "test:pending-parent-role-menu:action",
                IsEnabled = true,
                IsVisible = false
            });
        await dbContext.SaveChangesAsync();

        dbContext.RoleMenus.Add(new RoleMenu
        {
            RoleId = adminRole.Id,
            MenuId = parentMenuId
        });

        await InvokePrivateInitializerTaskAsync(
            initializer,
            "EnsureAdminPermissionIfParentAssignedAsync",
            parentMenuId,
            permissionMenuId,
            CancellationToken.None);

        Assert.Contains(
            dbContext.RoleMenus.Local,
            roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == permissionMenuId);
    }

    [Fact]
    public async Task DatabaseInitializer_Records_Baseline_Seed_Version_Once()
    {
        const string baselineSeedVersion = "202605280001-baseline-system-data";
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var initializer = scope.ServiceProvider.GetRequiredService<IMiniAdminDatabaseInitializer>();

        await initializer.InitializeAsync();

        var firstVersion = Assert.Single(await dbContext.DataSeedVersions
            .Where(version => version.Version == baselineSeedVersion)
            .ToArrayAsync());

        await initializer.InitializeAsync();

        var versions = await dbContext.DataSeedVersions
            .Where(version => version.Version == baselineSeedVersion)
            .ToArrayAsync();
        var secondVersion = Assert.Single(versions);

        Assert.Equal(firstVersion.Id, secondVersion.Id);
        Assert.Equal("系统基础菜单权限角色用户字典参数", secondVersion.Name);
    }

    [Fact]
    public async Task ScheduledJobList_Returns_AuditLogCleanup_Job()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");

        Assert.NotNull(json);
        var job = Assert.Single(json.Data.Items, item => item.JobKey == "audit-log-cleanup");
        Assert.Equal("清理审计日志", job.Name);
        Assert.True(job.IntervalSeconds >= 60);
    }

    [Fact]
    public async Task ScheduledJobList_Returns_StorageConsistencyCheck_Job()
    {
        await AuthorizeAsync();

        var json = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");

        Assert.NotNull(json);
        var job = Assert.Single(json.Data.Items, item => item.JobKey == "storage-consistency-check");
        Assert.Equal("检查文件存储一致性", job.Name);
        Assert.True(job.IntervalSeconds >= 60);
    }

    [Fact]
    public async Task ScheduledJob_RunOnce_Cleans_Expired_AuditLogs_And_Writes_Log()
    {
        await AuthorizeAsync();
        var expiredLogId = Guid.NewGuid();
        var retainedLogId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();

        dbContext.AuditLogs.AddRange(
            CreateAuditLog(expiredLogId, DateTimeOffset.UtcNow.AddDays(-91)),
            CreateAuditLog(retainedLogId, DateTimeOffset.UtcNow.AddDays(-89)));
        await dbContext.SaveChangesAsync();

        var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");
        Assert.NotNull(list);
        var cleanupJob = Assert.Single(list.Data.Items, item => item.JobKey == "audit-log-cleanup");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{cleanupJob.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();
        var runResult = await runResponse.Content.ReadFromJsonAsync<ApiEnvelope<ScheduledJobRunResultData>>();

        Assert.NotNull(runResult);
        Assert.Equal("Success", runResult.Data.Status);

        var remainingIds = await dbContext.AuditLogs
            .Where(log => log.Id == expiredLogId || log.Id == retainedLogId)
            .Select(log => log.Id)
            .ToArrayAsync();
        Assert.DoesNotContain(expiredLogId, remainingIds);
        Assert.Contains(retainedLogId, remainingIds);

        var logs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobLogData>>>(
            $"/system/scheduled-job/{cleanupJob.Id}/logs?page=1&pageSize=10");
        Assert.NotNull(logs);
        Assert.Contains(logs.Data.Items, log => log.Status == "Success" && log.TriggerType == "Manual");
    }

    [Fact]
    public async Task ScheduledJob_RunOnce_StorageConsistencyCheck_Detects_Missing_File_And_Writes_Log()
    {
        await AuthorizeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.ManagedFiles.RemoveRange(dbContext.ManagedFiles);
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = Guid.NewGuid(),
            OriginalName = "missing-storage-file.txt",
            StoredName = "missing-storage-file.txt",
            ContentType = "text/plain",
            Size = 12,
            StorageProvider = "local",
            StoragePath = $"missing/{Guid.NewGuid():N}.txt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");
        Assert.NotNull(list);
        var consistencyJob = Assert.Single(list.Data.Items, item => item.JobKey == "storage-consistency-check");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{consistencyJob.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();
        var runResult = await runResponse.Content.ReadFromJsonAsync<ApiEnvelope<ScheduledJobRunResultData>>();

        Assert.NotNull(runResult);
        Assert.Equal("Warning", runResult.Data.Status);
        Assert.Contains("缺失 1 个", runResult.Data.Message);

        var logs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobLogData>>>(
            $"/system/scheduled-job/{consistencyJob.Id}/logs?page=1&pageSize=10");
        Assert.NotNull(logs);
        Assert.Contains(logs.Data.Items, log => log.Status == "Warning" && log.TriggerType == "Manual");
    }

    [Fact]
    public async Task ScheduledJobLogDetails_Returns_Missing_File_Details_For_StorageConsistencyCheck()
    {
        await AuthorizeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.ManagedFiles.RemoveRange(dbContext.ManagedFiles);
        var missingFileId = Guid.NewGuid();
        var missingPath = $"missing/{Guid.NewGuid():N}.txt";
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = missingFileId,
            OriginalName = "missing-detail-file.txt",
            StoredName = "missing-detail-file.txt",
            ContentType = "text/plain",
            Size = 12,
            StorageProvider = "local",
            StoragePath = missingPath,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");
        Assert.NotNull(list);
        var consistencyJob = Assert.Single(list.Data.Items, item => item.JobKey == "storage-consistency-check");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{consistencyJob.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();

        var logs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobLogData>>>(
            $"/system/scheduled-job/{consistencyJob.Id}/logs?page=1&pageSize=10");
        Assert.NotNull(logs);
        var warningLog = logs.Data.Items.First(log => log.Status == "Warning" && log.TriggerType == "Manual");

        var details = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobLogDetailData>>>(
            $"/system/scheduled-job/logs/{warningLog.Id}/details?page=1&pageSize=10");

        Assert.NotNull(details);
        var detail = Assert.Single(details.Data.Items, item => item.TargetId == missingFileId.ToString());
        Assert.Equal(warningLog.Id, detail.LogId);
        Assert.Equal(consistencyJob.Id, detail.JobId);
        Assert.Equal("ManagedFile", detail.TargetType);
        Assert.Equal(missingFileId.ToString(), detail.TargetId);
        Assert.Equal("missing-detail-file.txt", detail.TargetName);
        Assert.Equal("local", detail.StorageProvider);
        Assert.Equal(missingPath, detail.StoragePath);
        Assert.Equal("Warning", detail.Status);
        Assert.Contains("文件不存在", detail.Message);
    }

    [Fact]
    public async Task FileException_StorageConsistencyCheck_Marks_Missing_File_And_Blocks_Download()
    {
        await AuthorizeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.ManagedFiles.RemoveRange(dbContext.ManagedFiles);
        var missingFileId = Guid.NewGuid();
        var missingFileName = $"missing-file-{Guid.NewGuid():N}.txt";
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = missingFileId,
            OriginalName = missingFileName,
            StoredName = missingFileName,
            ContentType = "text/plain",
            Size = 12,
            StorageProvider = "local",
            StoragePath = $"missing/{Guid.NewGuid():N}.txt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=10");
        Assert.NotNull(list);
        var consistencyJob = Assert.Single(list.Data.Items, item => item.JobKey == "storage-consistency-check");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{consistencyJob.Id}/run", null);
        runResponse.EnsureSuccessStatusCode();

        var files = await _client.GetFromJsonAsync<ApiEnvelope<PageData<FileData>>>(
            $"/system/file/list?page=1&pageSize=20&originalName={Uri.EscapeDataString(missingFileName)}");
        Assert.NotNull(files);
        var file = Assert.Single(files.Data.Items, item => item.Id == missingFileId.ToString());
        Assert.Equal("Missing", file.Status);

        var downloadResponse = await _client.GetAsync($"/system/file/{missingFileId}/download");
        Assert.Equal(HttpStatusCode.Conflict, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task FileException_Admin_Can_Mark_Missing_File_Invalid()
    {
        await AuthorizeAsync();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var missingFileId = Guid.NewGuid();
        var missingFileName = $"invalid-file-{Guid.NewGuid():N}.txt";
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = missingFileId,
            OriginalName = missingFileName,
            StoredName = missingFileName,
            ContentType = "text/plain",
            Size = 12,
            StorageProvider = "local",
            StoragePath = $"missing/{Guid.NewGuid():N}.txt",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var response = await _client.PostAsync($"/system/file/{missingFileId}/mark-invalid", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();

        Assert.NotNull(result);
        Assert.Equal("Invalid", result.Data.Status);

        var files = await _client.GetFromJsonAsync<ApiEnvelope<PageData<FileData>>>(
            $"/system/file/list?page=1&pageSize=20&originalName={Uri.EscapeDataString(missingFileName)}");
        Assert.NotNull(files);
        var file = Assert.Single(files.Data.Items, item => item.Id == missingFileId.ToString());
        Assert.Equal("Invalid", file.Status);

        var downloadResponse = await _client.GetAsync($"/system/file/{missingFileId}/download");
        Assert.Equal(HttpStatusCode.Conflict, downloadResponse.StatusCode);
    }

    [Fact]
    public async Task FileManagement_Uploads_Lists_And_Downloads_File_With_Local_Storage()
    {
        await AuthorizeAsync();
        var fileName = $"mini-admin-{Guid.NewGuid():N}.txt";
        const string fileContent = "hello mini admin file storage";

        using var multipart = new MultipartFormDataContent();
        using var fileContentPart = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(fileContent));
        fileContentPart.Headers.ContentType = new("text/plain");
        multipart.Add(fileContentPart, "file", fileName);

        var uploadResponse = await _client.PostAsync("/system/file/upload", multipart);
        uploadResponse.EnsureSuccessStatusCode();
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();

        try
        {
            Assert.NotNull(uploaded);
            Assert.Equal(fileName, uploaded.Data.OriginalName);
            Assert.Equal("local", uploaded.Data.StorageProvider);
            Assert.Equal("Normal", uploaded.Data.Status);

            var list = await _client.GetFromJsonAsync<ApiEnvelope<PageData<FileData>>>(
                $"/system/file/list?page=1&pageSize=20&originalName={Uri.EscapeDataString(fileName)}");
            Assert.NotNull(list);
            var listed = Assert.Single(list.Data.Items, file => file.Id == uploaded.Data.Id);
            Assert.Equal(fileContent.Length, listed.Size);

            var downloadResponse = await _client.GetAsync($"/system/file/{uploaded.Data.Id}/download");
            downloadResponse.EnsureSuccessStatusCode();
            var downloadedText = await downloadResponse.Content.ReadAsStringAsync();

            Assert.Equal(fileContent, downloadedText);
        }
        finally
        {
            if (uploaded is not null)
            {
                await _client.DeleteAsync($"/system/file/{uploaded.Data.Id}");
            }
        }
    }

    [Fact]
    public async Task FileManagement_Delete_Requires_Delete_Permission()
    {
        var roleCode = $"file-readonly-{Guid.NewGuid():N}";
        var userName = $"file-readonly-{Guid.NewGuid():N}";
        await AuthorizeAsync();

        var roleResponse = await _client.PostAsJsonAsync("/system/role", new
        {
            code = roleCode,
            name = "File Readonly",
            isEnabled = true
        });
        roleResponse.EnsureSuccessStatusCode();
        var role = await roleResponse.Content.ReadFromJsonAsync<ApiEnvelope<RoleListItemData>>();

        var menuTree = await _client.GetFromJsonAsync<ApiEnvelope<MenuTreeNodeData[]>>("/system/menu/tree");
        var roleList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<RoleListItemData>>>(
            "/system/role/list?page=1&pageSize=10&code=admin");
        var departmentTree = await _client.GetFromJsonAsync<ApiEnvelope<DepartmentItemData[]>>(
            "/system/department/list");
        var positionList = await _client.GetFromJsonAsync<ApiEnvelope<PageData<PositionData>>>(
            "/system/position/list?page=1&pageSize=10&code=manager");

        Assert.NotNull(role);
        Assert.NotNull(menuTree);
        Assert.NotNull(roleList);
        Assert.NotNull(departmentTree);
        Assert.NotNull(positionList);
        var systemMenu = Assert.Single(menuTree.Data, menu => menu.Name == "System");
        var fileMenu = Assert.Single(systemMenu.Children, menu => menu.Name == "FileManagement");
        var queryPermission = Assert.Single(fileMenu.Children, menu => menu.Name == "FileQueryPermission");
        var uploadPermission = Assert.Single(fileMenu.Children, menu => menu.Name == "FileUploadPermission");
        var downloadPermission = Assert.Single(fileMenu.Children, menu => menu.Name == "FileDownloadPermission");
        var adminRole = Assert.Single(roleList.Data.Items, item => item.Code == "admin");
        var headquarters = Assert.Single(departmentTree.Data, department => department.Code == "hq");
        var manager = Assert.Single(positionList.Data.Items, position => position.Code == "manager");

        var assignMenusResponse = await _client.PutAsJsonAsync($"/system/role/{role.Data.Id}/menus", new
        {
            menuIds = new[] { systemMenu.Id, fileMenu.Id, queryPermission.Id, uploadPermission.Id, downloadPermission.Id }
        });
        assignMenusResponse.EnsureSuccessStatusCode();

        var createUserResponse = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName = "File Readonly",
            password = "123456",
            departmentId = headquarters.Id,
            positionId = manager.Id,
            roleIds = new[] { role.Data.Id },
            isEnabled = true
        });
        createUserResponse.EnsureSuccessStatusCode();
        var createdUser = await createUserResponse.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();

        FileData? uploadedFile = null;
        try
        {
            Assert.NotNull(createdUser);
            using var multipart = new MultipartFormDataContent();
            using var fileContentPart = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("permission test"));
            fileContentPart.Headers.ContentType = new("text/plain");
            multipart.Add(fileContentPart, "file", "permission-test.txt");

            var uploadResponse = await _client.PostAsync("/system/file/upload", multipart);
            uploadResponse.EnsureSuccessStatusCode();
            var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();
            Assert.NotNull(uploaded);
            uploadedFile = uploaded.Data;

            var readonlyLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = userName,
                password = "123456"
            });
            readonlyLogin.EnsureSuccessStatusCode();
            var readonlyJson = await readonlyLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(readonlyJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                readonlyJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/file/{uploadedFile.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            await AuthorizeAsync();
            if (uploadedFile is not null)
            {
                await _client.DeleteAsync($"/system/file/{uploadedFile.Id}");
            }

            if (createdUser is not null)
            {
                await _client.DeleteAsync($"/system/user/{createdUser.Data.Id}");
            }

            await _client.DeleteAsync($"/system/role/{role.Data.Id}");
        }
    }

    [Fact]
    public async Task AdminRole_Removed_FileDeletePermission_Cannot_Delete_File()
    {
        await AuthorizeAsync();

        using var multipart = new MultipartFormDataContent();
        using var fileContentPart = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("admin permission test"));
        fileContentPart.Headers.ContentType = new("text/plain");
        multipart.Add(fileContentPart, "file", "admin-permission-test.txt");

        var uploadResponse = await _client.PostAsync("/system/file/upload", multipart);
        uploadResponse.EnsureSuccessStatusCode();
        var uploaded = await uploadResponse.Content.ReadFromJsonAsync<ApiEnvelope<FileData>>();

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var userAuthorizationCache = scope.ServiceProvider.GetRequiredService<IUserAuthorizationCache>();
        var adminRole = await dbContext.Roles.SingleAsync(role => role.Code == "admin");
        var adminUser = await dbContext.Users.SingleAsync(user => user.UserName == "admin");
        var fileDeletePermission = await dbContext.Menus.SingleAsync(
            menu => menu.PermissionCode == "system:file:delete");
        var adminFileDeleteRoleMenu = await dbContext.RoleMenus.SingleAsync(
            roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == fileDeletePermission.Id);

        dbContext.RoleMenus.Remove(adminFileDeleteRoleMenu);
        adminUser.SecurityStamp = Guid.NewGuid().ToString("N");
        await dbContext.SaveChangesAsync();
        await userAuthorizationCache.RemoveUserAsync(adminUser.Id, adminUser.UserName);

        try
        {
            Assert.NotNull(uploaded);

            var adminLogin = await _client.PostAsJsonAsync("/auth/login", new
            {
                username = "admin",
                password = "123456"
            });
            adminLogin.EnsureSuccessStatusCode();
            var adminJson = await adminLogin.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();
            Assert.NotNull(adminJson);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                adminJson.Data.AccessToken);

            var forbiddenDelete = await _client.DeleteAsync($"/system/file/{uploaded.Data.Id}");

            Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;

            if (!await dbContext.RoleMenus.AnyAsync(
                roleMenu => roleMenu.RoleId == adminRole.Id && roleMenu.MenuId == fileDeletePermission.Id))
            {
                dbContext.RoleMenus.Add(new RoleMenu
                {
                    RoleId = adminRole.Id,
                    MenuId = fileDeletePermission.Id
                });
                adminUser.SecurityStamp = Guid.NewGuid().ToString("N");
                await dbContext.SaveChangesAsync();
                await userAuthorizationCache.RemoveUserAsync(adminUser.Id, adminUser.UserName);
            }

            await AuthorizeAsync();

            if (uploaded is not null)
            {
                await _client.DeleteAsync($"/system/file/{uploaded.Data.Id}");
            }
        }
    }

    private static async Task InvokePrivateInitializerTaskAsync(
        IMiniAdminDatabaseInitializer initializer,
        string methodName,
        params object[] parameters)
    {
        var method = initializer.GetType()
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(method =>
                method.Name == methodName &&
                method.GetParameters().Length == parameters.Length);
        var task = Assert.IsAssignableFrom<Task>(method.Invoke(initializer, parameters));
        await task;
    }

    private static object CreateCodeGeneratorRequest(
        string moduleName,
        string businessName,
        string permissionPrefix,
        string routeSegment,
        string tenantMode = "Tenant",
        bool enableImportExport = false,
        bool enableWorkflow = false,
        string? workflowBusinessType = null)
    {
        return new
        {
            tableName = $"mini_{routeSegment.Replace('-', '_')}",
            moduleName,
            businessName,
            routePath = $"/business/{routeSegment}",
            parentMenuId = (string?)null,
            permissionPrefix,
            tenantMode,
            enableImportExport,
            enableWorkflow,
            workflowBusinessType,
            fields = new[]
            {
                new
                {
                    columnName = "customer_name",
                    propertyName = "CustomerName",
                    displayName = "客户名称",
                    dotNetType = "string",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = true,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Input",
                    dictionaryCode = (string?)null,
                    sort = 1
                }
            }
        };
    }

    private static object CreateDepartmentScopedCodeGeneratorRequest(
        string moduleName,
        string businessName,
        string permissionPrefix,
        string routeSegment)
    {
        return new
        {
            tableName = $"mini_{routeSegment.Replace('-', '_')}",
            moduleName,
            businessName,
            routePath = $"/business/{routeSegment}",
            parentMenuId = (string?)null,
            permissionPrefix,
            tenantMode = "Tenant",
            dataScopeMode = "Department",
            dataScopeField = "DepartmentId",
            enableAudit = true,
            fields = new object[]
            {
                new
                {
                    columnName = "customer_name",
                    propertyName = "CustomerName",
                    displayName = "客户名称",
                    dotNetType = "string",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = true,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Input",
                    dictionaryCode = (string?)null,
                    sort = 1
                },
                new
                {
                    columnName = "department_id",
                    propertyName = "DepartmentId",
                    displayName = "所属部门",
                    dotNetType = "Guid",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = false,
                    listVisible = false,
                    queryVisible = false,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Input",
                    dictionaryCode = (string?)null,
                    sort = 2
                }
            }
        };
    }

    private static object CreateAdvancedCodeGeneratorRequest(
        string moduleName,
        string businessName,
        string permissionPrefix,
        string routeSegment)
    {
        return new
        {
            tableName = $"biz_{routeSegment.Replace('-', '_')}",
            moduleName,
            businessName,
            routePath = $"/business/{routeSegment}",
            parentMenuId = (string?)null,
            permissionPrefix,
            tenantMode = "Tenant",
            fields = new object[]
            {
                new
                {
                    columnName = "order_no",
                    propertyName = "OrderNo",
                    displayName = "订单编号",
                    dotNetType = "string",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = true,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Input",
                    dictionaryCode = (string?)null,
                    sort = 1,
                    queryMode = "Equals",
                    maxLength = 64,
                    isUnique = true,
                    defaultValue = (string?)null
                },
                new
                {
                    columnName = "order_name",
                    propertyName = "OrderName",
                    displayName = "订单名称",
                    dotNetType = "string",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = true,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Textarea",
                    dictionaryCode = (string?)null,
                    sort = 2,
                    queryMode = "Contains",
                    maxLength = 80,
                    isUnique = false,
                    defaultValue = "新订单"
                },
                new
                {
                    columnName = "status",
                    propertyName = "Status",
                    displayName = "状态",
                    dotNetType = "string",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = true,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "Select",
                    dictionaryCode = "order_status",
                    sort = 3,
                    queryMode = "Equals",
                    maxLength = 32,
                    isUnique = false,
                    defaultValue = "pending"
                },
                new
                {
                    columnName = "paid_at",
                    propertyName = "PaidAt",
                    displayName = "支付时间",
                    dotNetType = "DateTimeOffset?",
                    tsType = "string",
                    isPrimaryKey = false,
                    isRequired = false,
                    listVisible = true,
                    queryVisible = true,
                    createVisible = true,
                    updateVisible = true,
                    controlType = "DatePicker",
                    dictionaryCode = (string?)null,
                    sort = 4,
                    queryMode = "Range",
                    maxLength = (int?)null,
                    isUnique = false,
                    defaultValue = (string?)null
                }
            }
        };
    }

    private Task AuthorizeAsync()
    {
        return AuthorizeAsync("admin", "123456");
    }

    private async Task AuthorizeAsync(string username, string password, string? tenantCode = null)
    {
        var response = await LoginAsync(username, password, tenantCode);
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        Assert.NotNull(json);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", json.Data.AccessToken);
    }

    private async Task RestoreDefaultSecurityPolicyAsync()
    {
        var response = await _client.PutAsJsonAsync("/system/security-policy", new
        {
            captchaRequiredFailures = 3,
            lockoutFailures = 5,
            lockoutMinutes = 10,
            captchaExpireSeconds = 120,
            onlineActiveTimeoutMinutes = 30,
            onlineTouchThrottleSeconds = 30,
            staleUserDays = 90
        });
        response.EnsureSuccessStatusCode();
    }

    private async Task CreateMissingManagedFileAsync()
    {
        var fileId = Guid.NewGuid();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.ManagedFiles.Add(new ManagedFile
        {
            Id = fileId,
            OriginalName = $"missing-alert-{fileId:N}.txt",
            StoredName = $"{fileId:N}.txt",
            ContentType = "text/plain",
            Size = 12,
            StorageProvider = "Local",
            StoragePath = $"missing-alert/{fileId:N}.txt",
            Status = "Missing",
            CreatedAt = DateTimeOffset.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<AlertRuleData> GetAlertRuleByCodeAsync(string code)
    {
        var rules = await _client.GetFromJsonAsync<ApiEnvelope<PageData<AlertRuleData>>>(
            "/system/alert-rule/list?page=1&pageSize=20");
        Assert.NotNull(rules);
        return Assert.Single(rules.Data.Items, item => item.Code == code);
    }

    private async Task<AlertRuleData> UpdateAlertRuleAsync(
        AlertRuleData rule,
        bool enabled,
        bool notifyEnabled)
    {
        var response = await _client.PutAsJsonAsync($"/system/alert-rule/{rule.Id}", new
        {
            level = rule.Level,
            threshold = rule.Threshold,
            windowMinutes = rule.WindowMinutes,
            enabled,
            notifyEnabled,
            emailEnabled = rule.EmailEnabled,
            recipientRoleIds = rule.Recipients
                .Where(recipient => recipient.RecipientType == "Role")
                .Select(recipient => recipient.RecipientId)
                .ToArray(),
            recipientUserIds = rule.Recipients
                .Where(recipient => recipient.RecipientType == "User")
                .Select(recipient => recipient.RecipientId)
                .ToArray(),
            remark = rule.Remark
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<AlertRuleData>>();
        Assert.NotNull(json);
        return json.Data;
    }

    private async Task<AlertRuleData> UpdateAlertRuleRecipientsAsync(
        AlertRuleData rule,
        string[] roleIds,
        string[] userIds,
        bool emailEnabled)
    {
        var response = await _client.PutAsJsonAsync($"/system/alert-rule/{rule.Id}", new
        {
            level = rule.Level,
            threshold = rule.Threshold,
            windowMinutes = rule.WindowMinutes,
            enabled = rule.Enabled,
            notifyEnabled = rule.NotifyEnabled,
            emailEnabled,
            recipientRoleIds = roleIds,
            recipientUserIds = userIds,
            remark = rule.Remark
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<AlertRuleData>>();
        Assert.NotNull(json);
        return json.Data;
    }

    private async Task RestoreAlertRuleAsync(AlertRuleData rule)
    {
        var response = await _client.PutAsJsonAsync($"/system/alert-rule/{rule.Id}", new
        {
            level = rule.Level,
            threshold = rule.Threshold,
            windowMinutes = rule.WindowMinutes,
            enabled = rule.Enabled,
            notifyEnabled = rule.NotifyEnabled,
            emailEnabled = rule.EmailEnabled,
            recipientRoleIds = rule.Recipients
                .Where(recipient => recipient.RecipientType == "Role")
                .Select(recipient => recipient.RecipientId)
                .ToArray(),
            recipientUserIds = rule.Recipients
                .Where(recipient => recipient.RecipientType == "User")
                .Select(recipient => recipient.RecipientId)
                .ToArray(),
            remark = rule.Remark
        });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    private async Task ClearAlertsAndNotificationsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.UserNotifications.RemoveRange(dbContext.UserNotifications);
        dbContext.Alerts.RemoveRange(dbContext.Alerts);
        await dbContext.SaveChangesAsync();
    }

    private async Task SeedAdminNotificationsAsync()
    {
        var adminUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var now = DateTimeOffset.UtcNow;
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        dbContext.UserNotifications.AddRange(
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminUserId,
                Title = "系统告警一",
                Message = "第一条未读系统告警",
                Category = "SystemAlert",
                Level = "Warning",
                Link = "/system/alert",
                SourceType = "Alert",
                SourceId = Guid.NewGuid().ToString(),
                IsRead = false,
                CreatedAt = now.AddMinutes(-2)
            },
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminUserId,
                Title = "系统告警二",
                Message = "第二条未读系统告警",
                Category = "SystemAlert",
                Level = "Critical",
                Link = "/system/alert",
                SourceType = "Alert",
                SourceId = Guid.NewGuid().ToString(),
                IsRead = false,
                CreatedAt = now.AddMinutes(-1)
            },
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminUserId,
                Title = "业务通知",
                Message = "一条未读业务通知",
                Category = "Business",
                Level = "Info",
                SourceType = "Business",
                SourceId = Guid.NewGuid().ToString(),
                IsRead = false,
                CreatedAt = now
            },
            new UserNotification
            {
                Id = Guid.NewGuid(),
                UserId = adminUserId,
                Title = "已读通知",
                Message = "一条已读通知",
                Category = "SystemAlert",
                Level = "Info",
                SourceType = "Alert",
                SourceId = Guid.NewGuid().ToString(),
                IsRead = true,
                CreatedAt = now.AddMinutes(-3),
                ReadAt = now.AddMinutes(-2)
            });
        await dbContext.SaveChangesAsync();
    }

    private async Task<ScheduledJobRunResultData> RunAlertScanJobAsync()
    {
        var jobs = await _client.GetFromJsonAsync<ApiEnvelope<PageData<ScheduledJobData>>>(
            "/system/scheduled-job/list?page=1&pageSize=20&jobKey=alert-scan");
        Assert.NotNull(jobs);
        var alertScan = Assert.Single(jobs.Data.Items, job => job.JobKey == "alert-scan");

        var runResponse = await _client.PostAsync($"/system/scheduled-job/{alertScan.Id}/run", null);
        Assert.True(runResponse.IsSuccessStatusCode, await runResponse.Content.ReadAsStringAsync());
        var runResult = await runResponse.Content.ReadFromJsonAsync<ApiEnvelope<ScheduledJobRunResultData>>();
        Assert.NotNull(runResult);
        return runResult.Data;
    }

    private Task<HttpResponseMessage> LoginAsync()
    {
        return LoginAsync("admin", "123456");
    }

    private Task<HttpResponseMessage> LoginAsync(string username, string password, string? tenantCode = null)
    {
        return _client.PostAsJsonAsync("/auth/login", new
        {
            username,
            password,
            tenantCode
        });
    }

    private async Task SetDemoTenantStatusAsync(TenantStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MiniAdminDbContext>();
        var tenant = await dbContext.Tenants.SingleAsync(x => x.Code == "demo");
        tenant.Status = status;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task UserExport_Returns_Xlsx_For_Current_Filter()
    {
        await AuthorizeAsync();

        var response = await _client.GetAsync("/system/user/export?userName=admin");

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "mini-admin-users.xlsx",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task UserImport_Creates_User_From_Xlsx()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"import-{unique}";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateUserImportWorkbook(
            [
                [
                    "用户名",
                    "姓名",
                    "初始密码",
                    "部门编码",
                    "岗位编码",
                    "角色编码",
                    "启用状态"
                ],
                [
                    userName,
                    "Imported User",
                    "Import123",
                    "hq",
                    "manager",
                    "admin",
                    "启用"
                ]
            ]));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "users.xlsx");

        var response = await _client.PostAsync("/system/user/import", content);

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserImportResultData>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Data.CreatedCount);
        Assert.Empty(result.Data.Errors);

        var users = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            $"/system/user/list?page=1&pageSize=10&userName={userName}");
        Assert.NotNull(users);
        var importedUser = Assert.Single(users.Data.Items);
        Assert.Equal(userName, importedUser.UserName);
        Assert.Equal("Imported User", importedUser.RealName);
        Assert.Equal("总部", importedUser.DepartmentName);
        Assert.Equal("管理员", importedUser.PositionName);
        Assert.Contains("admin", importedUser.Roles);

        await _client.DeleteAsync($"/system/user/{importedUser.Id}");
    }

    [Fact]
    public async Task UserImportPreview_Validates_Without_Creating_User()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var userName = $"preview-{unique}";
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateUserImportWorkbook(
            [
                [
                    "用户名",
                    "姓名",
                    "初始密码",
                    "部门编码",
                    "岗位编码",
                    "角色编码",
                    "启用状态"
                ],
                [
                    userName,
                    "Preview User",
                    "Preview123",
                    "hq",
                    "manager",
                    "admin",
                    "启用"
                ]
            ]));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "users.xlsx");

        var response = await _client.PostAsync("/system/user/import/preview", content);

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        var result = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserImportResultData>>();
        Assert.NotNull(result);
        Assert.Equal(1, result.Data.CreatedCount);
        Assert.Empty(result.Data.Errors);

        var users = await _client.GetFromJsonAsync<ApiEnvelope<PageData<UserListItemData>>>(
            $"/system/user/list?page=1&pageSize=10&userName={userName}");
        Assert.NotNull(users);
        Assert.Empty(users.Data.Items);
    }

    [Fact]
    public async Task UserImportErrorReport_Returns_Xlsx_For_Failed_Rows()
    {
        await AuthorizeAsync();
        var unique = Guid.NewGuid().ToString("N")[..8];
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(CreateUserImportWorkbook(
            [
                [
                    "用户名",
                    "姓名",
                    "初始密码",
                    "部门编码",
                    "岗位编码",
                    "角色编码",
                    "启用状态"
                ],
                [
                    $"error-report-{unique}",
                    "Error Report User",
                    "Report123",
                    "missing-department",
                    "manager",
                    "admin",
                    "启用"
                ]
            ]));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, "file", "users.xlsx");

        var response = await _client.PostAsync("/system/user/import/error-report", content);

        Assert.True(
            response.IsSuccessStatusCode,
            await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "mini-admin-user-import-errors.xlsx",
            response.Content.Headers.ContentDisposition?.FileNameStar ??
            response.Content.Headers.ContentDisposition?.FileName);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 2);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    private async Task<UserListItemData> CreateTestUserAsync(
        string userName,
        string realName,
        string departmentId,
        string positionId,
        string roleId)
    {
        var response = await _client.PostAsJsonAsync("/system/user", new
        {
            userName,
            realName,
            password = "123456",
            departmentId,
            positionId,
            roleIds = new[] { roleId },
            isEnabled = true
        });
        response.EnsureSuccessStatusCode();
        var user = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserListItemData>>();
        Assert.NotNull(user);

        return user.Data;
    }

    private static byte[] CreateUserImportWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteArchiveEntry(
                archive,
                "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                </Types>
                """);
            WriteArchiveEntry(
                archive,
                "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteArchiveEntry(
                archive,
                "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Users" sheetId="1" r:id="rId1"/>
                  </sheets>
                </workbook>
                """);
            WriteArchiveEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);

            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            var sheetData = new XElement(spreadsheet + "sheetData",
                rows.Select((row, rowIndex) => new XElement(spreadsheet + "row",
                    new XAttribute("r", rowIndex + 1),
                    row.Select((value, columnIndex) => new XElement(spreadsheet + "c",
                        new XAttribute("r", $"{GetColumnName(columnIndex + 1)}{rowIndex + 1}"),
                        new XAttribute("t", "inlineStr"),
                        new XElement(spreadsheet + "is",
                            new XElement(spreadsheet + "t", value)))))));
            var worksheet = new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement(spreadsheet + "worksheet", sheetData));
            WriteArchiveEntry(archive, "xl/worksheets/sheet1.xml", worksheet.ToString(SaveOptions.DisableFormatting));
        }

        return stream.ToArray();
    }

    private static void WriteArchiveEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    private static string GetColumnName(int columnNumber)
    {
        var dividend = columnNumber;
        var columnName = string.Empty;
        while (dividend > 0)
        {
            var modulo = (dividend - 1) % 26;
            columnName = Convert.ToChar('A' + modulo) + columnName;
            dividend = (dividend - modulo) / 26;
        }

        return columnName;
    }

    private static AuditLog CreateAuditLog(Guid id, DateTimeOffset createdAt)
    {
        return new AuditLog
        {
            Id = id,
            UserName = "retention-test",
            Method = "POST",
            Path = "/system/retention-test",
            Module = "System",
            Action = "Create",
            StatusCode = 200,
            IsSuccess = true,
            ElapsedMilliseconds = 1,
            RequestBody = "{}",
            CreatedAt = createdAt
        };
    }

    private static AuditLog CreateAuditLog(Guid id, string userName, string path)
    {
        return new AuditLog
        {
            Id = id,
            UserName = userName,
            Method = "GET",
            Path = path,
            Module = "System",
            Action = "Query",
            StatusCode = 200,
            IsSuccess = true,
            ElapsedMilliseconds = 1,
            RequestBody = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);
    private sealed record LoginData(
        string AccessToken,
        string SessionId,
        string? TenantId,
        string? TenantCode);
    private sealed record LoginFailureData(bool CaptchaRequired, int? LockRemainingSeconds);
    private sealed record PermissionDiagnosticsData(
        PermissionDiagnosticsUserData User,
        IReadOnlyList<PermissionDiagnosticsRoleData> Roles,
        IReadOnlyList<string> PermissionCodes,
        IReadOnlyList<PermissionDiagnosticsMenuData> MenuItems,
        PermissionDiagnosticsDataScopeData DataScope,
        PermissionDiagnosticsCacheData Cache,
        PermissionDiagnosticsTenantData Tenant,
        PermissionDiagnosticsEffectiveData Effective,
        IReadOnlyList<PermissionDiagnosticsWarningData> Warnings);
    private sealed record PermissionDiagnosticsUserData(
        string Id,
        string UserName,
        string RealName,
        string? DepartmentName,
        string? PositionName,
        bool IsEnabled);
    private sealed record PermissionDiagnosticsRoleData(
        string Id,
        string Code,
        string Name,
        string DataScope,
        bool IsEnabled,
        int MenuCount,
        int VisibleMenuCount,
        int ButtonPermissionCount,
        IReadOnlyList<string>? CustomDepartmentIds);
    private sealed record PermissionDiagnosticsMenuData(
        string Id,
        string Title,
        string Path,
        string? PermissionCode,
        bool IsVisible);
    private sealed record PermissionDiagnosticsDataScopeData(
        string Level,
        string Description,
        string? DepartmentId,
        IReadOnlyList<string> DepartmentIds,
        IReadOnlyList<string> DepartmentNames);
    private sealed record PermissionDiagnosticsCacheData(
        string SecurityStampKey,
        string PermissionCodesKey,
        string MenusKey);
    private sealed record PermissionDiagnosticsTenantData(
        bool IsTenant,
        string? TenantId,
        string? TenantCode,
        string? TenantName,
        string? PackageId,
        string? PackageName,
        int PackageMenuCount,
        bool IsPackageLimited);
    private sealed record PermissionDiagnosticsEffectiveData(
        int RoleMenuCount,
        int PackageMenuCount,
        int FinalMenuCount,
        int VisibleMenuCount,
        int ButtonPermissionCount,
        int PermissionCodeCount);
    private sealed record PermissionDiagnosticsWarningData(
        string Code,
        string Level,
        string Message,
        string Suggestion);
    private sealed record UserInfoData(
        string UserId,
        string Username,
        string RealName,
        string? DepartmentId,
        string? DepartmentName,
        string? PositionId,
        string? PositionName,
        string[] Roles);
    private sealed record MenuData(string Name, string Path, MenuData[] Children);
    private sealed record SystemMonitorOverviewData(
        SystemMonitorApiData Api,
        SystemMonitorCpuData Cpu,
        SystemMonitorMemoryData Memory,
        SystemMonitorApplicationData Application,
        SystemMonitorServerData Server,
        IReadOnlyList<SystemMonitorDependencyData> Dependencies,
        SystemMonitorRecentData Recent);
    private sealed record SystemMonitorApiData(string Status, DateTimeOffset Timestamp);
    private sealed record SystemMonitorCpuData(int ProcessorCount, int ThreadCount);
    private sealed record SystemMonitorMemoryData(
        long TotalPhysicalMemoryBytes,
        long AvailablePhysicalMemoryBytes,
        long UsedPhysicalMemoryBytes,
        double PhysicalMemoryUsedPercent,
        long WorkingSetBytes,
        long ManagedHeapBytes,
        long GcTotalMemoryBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections);
    private sealed record SystemMonitorApplicationData(
        string Environment,
        string RuntimeVersion,
        DateTimeOffset StartedAt,
        long UptimeSeconds,
        string ContentRootPath);
    private sealed record SystemMonitorServerData(
        string MachineName,
        string OperatingSystem,
        string Architecture);
    private sealed record SystemMonitorDependencyData(
        string Name,
        string Status,
        string Description,
        long? ElapsedMilliseconds);
    private sealed record SystemMonitorRecentData(
        int FailedScheduledJobCount,
        int FailedAuditLogCount,
        int OnlineUserCount,
        int AbnormalFileCount);
    private sealed record SecurityCenterOverviewData(
        SecurityAccountSummaryData Account,
        SecurityLoginSummaryData Login,
        SecurityPermissionSummaryData Permission,
        SecuritySessionSummaryData Session,
        SecurityEventData[] RecentEvents);
    private sealed record SecurityAccountSummaryData(
        int TotalUserCount,
        int EnabledUserCount,
        int DisabledUserCount,
        int LockedUserCount,
        int StaleUserCount);
    private sealed record SecurityLoginSummaryData(
        int FailedLoginCount24h,
        int FailedUserCount24h,
        int FailedIpCount24h);
    private sealed record SecurityPermissionSummaryData(
        int PermissionChangeCount24h,
        SecurityEventData[] RecentHighRiskEvents);
    private sealed record SecuritySessionSummaryData(
        int OnlineUserCount,
        SecurityEventData[] RecentForceLogoutEvents);
    private sealed record SecurityEventData(
        string Id,
        string EventType,
        string Level,
        string? UserId,
        string? UserName,
        string? IpAddress,
        string? UserAgent,
        string Title,
        string Description,
        string? RelatedEntityType,
        string? RelatedEntityId,
        DateTimeOffset CreatedAt);
    private sealed record SecurityPolicyData(
        int CaptchaRequiredFailures,
        int LockoutFailures,
        int LockoutMinutes,
        int CaptchaExpireSeconds,
        int OnlineActiveTimeoutMinutes,
        int OnlineTouchThrottleSeconds,
        int StaleUserDays);
    private sealed record PageData<T>(T[] Items, int Total);
    private sealed record UserListItemData(
        string Id,
        string UserName,
        string RealName,
        string? DepartmentId,
        string? DepartmentName,
        string? PositionId,
        string? PositionName,
        string[] Roles,
        int Status,
        int? LoginLockRemainingSeconds);
    private sealed record RoleListItemData(
        string Id,
        string Code,
        string Name,
        string DataScope,
        int Status,
        IReadOnlyList<string>? CustomDepartmentIds = null);
    private sealed record MenuTreeNodeData(string Id, string Name, string Title, MenuTreeNodeData[] Children);
    private sealed record MenuManagementItemData(
        string Id,
        string? ParentId,
        string Name,
        string Path,
        string? Component,
        string? Redirect,
        string Title,
        string? Icon,
        int Order,
        bool AffixTab,
        string? PermissionCode,
        bool IsEnabled,
        bool IsVisible,
        MenuManagementItemData[] Children);
    private sealed record DepartmentItemData(
        string Id,
        string? ParentId,
        string Code,
        string Name,
        string? Leader,
        string? Phone,
        int Order,
        bool IsEnabled,
        DepartmentItemData[] Children);
    private sealed record DictionaryTypeData(
        string Id,
        string Code,
        string Name,
        int Order,
        bool IsEnabled,
        DictionaryItemData[] Items);
    private sealed record DictionaryItemData(
        string Id,
        string TypeId,
        string Label,
        string Value,
        string? Color,
        int Order,
        bool IsEnabled);
    private sealed record SystemParameterData(
        string Id,
        string Key,
        string Name,
        string Value,
        string Group,
        string? Remark,
        int Order,
        bool IsEnabled);
    private sealed record CodeGeneratorPreviewData(
        CodeGeneratorPreviewFileData[] Files,
        string[] PermissionCodes,
        bool HasConflicts,
        CodeGeneratorInstallPlanData InstallPlan);
    private sealed record CodeGeneratorInstallPlanData(
        bool TableExists,
        string? CreateTableSql,
        CodeGeneratorInstallStepData[] Steps);
    private sealed record CodeGeneratorInstallStepData(
        string Key,
        string Title,
        string Description,
        string Status);
    private sealed record CodeGeneratorPreviewFileData(
        string RelativePath,
        string Content,
        bool HasConflict);
    private sealed record CodeGenerationHistoryData(
        string Id,
        string TableName,
        string ModuleName,
        string BusinessName,
        string PermissionPrefix,
        string TenantMode,
        string Status,
        string? ErrorMessage,
        CodeGeneratorPreviewFileData[] Files,
        DateTimeOffset CreatedAt);
    private sealed record CodeGenerationHistoryDetailData(
        string Id,
        string TableName,
        string ModuleName,
        string BusinessName,
        string PermissionPrefix,
        string TenantMode,
        string Status,
        string? ErrorMessage,
        string? OperatorUserName,
        CodeGeneratorPreviewRequestData Preview,
        CodeGeneratorPreviewFileData[] Files,
        CodeGeneratorInstallPlanData InstallPlan,
        DateTimeOffset CreatedAt);
    private sealed record CodeGeneratorRollbackData(
        string Id,
        string Status,
        int DeletedFileCount,
        int DeletedMenuCount,
        bool TableDropped,
        bool TableDropSkipped,
        string? TableDropMessage);
    private sealed record CodeGeneratorPreviewRequestData(
        string TableName,
        string ModuleName,
        string BusinessName,
        string RoutePath,
        string? ParentMenuId,
        string PermissionPrefix,
        string TenantMode,
        CodeGeneratorFieldConfigData[] Fields);
    private sealed record CodeGeneratorFieldConfigData(
        string ColumnName,
        string PropertyName,
        string DisplayName,
        string DotNetType,
        string TsType,
        bool IsPrimaryKey,
        bool IsRequired,
        bool ListVisible,
        bool QueryVisible,
        bool CreateVisible,
        bool UpdateVisible,
        string ControlType,
        string? DictionaryCode,
        int Sort,
        string QueryMode,
        int? MaxLength,
        bool IsUnique,
        string? DefaultValue);
    private sealed record PositionData(
        string Id,
        string Code,
        string Name,
        int Order,
        string? Remark,
        bool IsEnabled);
    private sealed record NoticeData(
        string Id,
        string Title,
        string Type,
        string Content,
        bool IsPublished,
        DateTimeOffset? PublishedAt,
        DateTimeOffset CreatedAt);
    private sealed record AuditLogData(
        string Id,
        string? UserId,
        string? UserName,
        string Method,
        string Path,
        string? QueryString,
        string Module,
        string Action,
        string? ResourceId,
        int StatusCode,
        bool IsSuccess,
        long ElapsedMilliseconds,
        string? IpAddress,
        string? UserAgent,
        string RequestBody,
        string? ErrorMessage,
        DateTimeOffset CreatedAt,
        IReadOnlyList<AuditEntityChangeData> EntityChanges);
    private sealed record AuditEntityChangeData(
        string Id,
        string AuditLogId,
        string EntityName,
        string EntityId,
        string OperationType,
        string? BeforeJson,
        string? AfterJson,
        string DiffJson,
        DateTimeOffset CreatedAt);
    private sealed record FileData(
        string Id,
        string OriginalName,
        string StoredName,
        string ContentType,
        long Size,
        string StorageProvider,
        string StoragePath,
        string Status,
        DateTimeOffset CreatedAt);
    private sealed record LoginLogData(
        string Id,
        string? UserId,
        string UserName,
        string? RealName,
        string? IpAddress,
        string? UserAgent,
        bool IsSuccess,
        string Message,
        DateTimeOffset CreatedAt);
    private sealed record OnlineUserData(
        string SessionId,
        string UserId,
        string UserName,
        string RealName,
        string? IpAddress,
        string? UserAgent,
        string? DeviceName,
        string? BrowserName,
        DateTimeOffset LoginAt,
        DateTimeOffset LastActiveAt);
    private sealed record TenantData(
        string Id,
        string Code,
        string Name,
        string Status,
        string? InitializationTemplateCode,
        string? InitializationStatus,
        DateTimeOffset? InitializedAt,
        string? InitializationError,
        string? ContactName,
        string? ContactPhone,
        string? ContactEmail,
        DateTimeOffset? ExpireAt,
        string? Remark,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
    private sealed record TenantLoginOptionData(string Code, string Name);
    private sealed record ScheduledJobData(
        string Id,
        string JobKey,
        string Name,
        string? Description,
        int IntervalSeconds,
        bool IsEnabled,
        string LastStatus,
        string? LastMessage,
        DateTimeOffset? LastRunAt,
        DateTimeOffset? NextRunAt);
    private sealed record ScheduledJobRunResultData(
        string JobId,
        string JobKey,
        string Status,
        string Message,
        long ElapsedMilliseconds);
    private sealed record ScheduledJobLogData(
        string Id,
        string JobId,
        string JobKey,
        string JobName,
        string TriggerType,
        string Status,
        string Message,
        DateTimeOffset StartedAt,
        DateTimeOffset FinishedAt,
        long ElapsedMilliseconds);
    private sealed record ScheduledJobLogDetailData(
        string Id,
        string LogId,
        string JobId,
        string JobKey,
        string DetailType,
        string TargetType,
        string? TargetId,
        string? TargetName,
        string? StorageProvider,
        string? StoragePath,
        string Status,
        string Message,
        DateTimeOffset CreatedAt);
    private sealed record AlertData(
        string Id,
        string Type,
        string Level,
        string Title,
        string Content,
        string Source,
        string Status,
        DateTimeOffset FirstTriggeredAt,
        DateTimeOffset LastTriggeredAt,
        DateTimeOffset? RecoveredAt,
        string? AcknowledgedBy,
        DateTimeOffset? AcknowledgedAt,
        string? AcknowledgeRemark,
        int TriggerCount);
    private sealed record AlertRuleData(
        string Id,
        string Code,
        string Name,
        string Description,
        string Metric,
        string Operator,
        decimal Threshold,
        int WindowMinutes,
        string Level,
        bool Enabled,
        bool NotifyEnabled,
        bool EmailEnabled,
        int Sort,
        string? Remark,
        AlertRuleRecipientData[] Recipients,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
    private sealed record AlertRuleRecipientData(
        string Id,
        string RecipientType,
        string RecipientId,
        string RecipientName);
    private sealed record UserNotificationListData(
        UserNotificationData[] Items,
        int Total,
        int UnreadCount);
    private sealed record UserNotificationData(
        string Id,
        string Title,
        string Message,
        string Category,
        string Level,
        string? Link,
        string SourceType,
        string SourceId,
        bool IsRead,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadAt);
    private sealed record NotificationDeliveryData(
        string Id,
        string Channel,
        string UserId,
        string RecipientAddress,
        string SourceType,
        string SourceId,
        string Status,
        string? ErrorMessage);
    private sealed record UserImportResultData(int CreatedCount, UserImportErrorData[] Errors);
    private sealed record UserImportErrorData(int RowNumber, string UserName, string Message);
    private sealed record PositionImportResultData(int CreatedCount, PositionImportErrorData[] Errors);
    private sealed record PositionImportErrorData(int RowNumber, string Code, string Message);

    private sealed class TestWorkflowBusinessStateHandler : IWorkflowBusinessStateHandler
    {
        public Task HandleAsync(
            WorkflowInstanceDto instance,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
