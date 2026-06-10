# Official Vben Login Loop Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first visible MiniAdmin milestone: official Vben Admin can log in to the MiniAdmin backend using fake data.

**Architecture:** The backend keeps the existing layered structure. `MiniAdmin.Api` exposes HTTP endpoints, `MiniAdmin.Application.Contracts` owns DTOs and service interfaces, `MiniAdmin.Application` owns fake login/user/menu use cases, and `MiniAdmin.Shared` owns the unified API response wrapper. The official Vben frontend stays close to default; only API base URL, backend access mode, and menu endpoint wiring are changed if required.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, xUnit for backend verification, official Vben Admin with Vue 3, Vite, TypeScript, pnpm, Ant Design Vue app variant.

---

## Source References

- Vben login docs: https://doc.vben.pro/guide/in-depth/login.html
- Vben access docs: https://doc.vben.pro/guide/in-depth/access.html
- Design spec: `docs/superpowers/specs/2026-05-26-miniadmin-vben-learning-design.md`

Vben default backend contract for this milestone:

```text
POST /auth/login  -> { code: 0, data: { accessToken: "..." }, message: "ok" }
GET  /user/info   -> { code: 0, data: { realName: "...", roles: [...] }, message: "ok" }
GET  /auth/codes  -> { code: 0, data: ["system:user:query"], message: "ok" }
GET  /menu/all    -> { code: 0, data: [backend menu routes], message: "ok" }
```

## File Structure

Backend files:

- Create `src/MiniAdmin.Shared/ApiResponse.cs`: unified response wrapper expected by Vben.
- Create `src/MiniAdmin.Application.Contracts/Auth/LoginRequest.cs`: login request DTO.
- Create `src/MiniAdmin.Application.Contracts/Auth/LoginResult.cs`: login response DTO.
- Create `src/MiniAdmin.Application.Contracts/Users/CurrentUserDto.cs`: current user DTO.
- Create `src/MiniAdmin.Application.Contracts/Menus/VbenMenuDto.cs`: backend menu DTOs for Vben.
- Create `src/MiniAdmin.Application.Contracts/Auth/IAuthAppService.cs`: authentication contract.
- Create `src/MiniAdmin.Application.Contracts/Users/IUserAppService.cs`: current-user contract.
- Create `src/MiniAdmin.Application.Contracts/Menus/IMenuAppService.cs`: menu contract.
- Create `src/MiniAdmin.Application/Auth/FakeAuthAppService.cs`: fake login and permission codes.
- Create `src/MiniAdmin.Application/Users/FakeUserAppService.cs`: fake current user.
- Create `src/MiniAdmin.Application/Menus/FakeMenuAppService.cs`: fake backend menus.
- Modify `src/MiniAdmin.Api/Program.cs`: register services, CORS, and Vben-compatible endpoints.

Backend test files:

- Create `tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`: xUnit test project.
- Create `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`: integration tests for the four backend endpoints.
- Modify `MiniAdmin.slnx`: add test project.

Frontend files after cloning official Vben:

- Clone official repository into `frontend/vue-vben-admin`.
- Inspect `frontend/vue-vben-admin/apps/web-antd/vite.config.mts`: configure `/api` proxy to MiniAdmin API.
- Inspect `frontend/vue-vben-admin/apps/web-antd/src/preferences.ts`: set `app.accessMode` to `backend`.
- Inspect `frontend/vue-vben-admin/apps/web-antd/src/api/core/menu.ts`: confirm or adjust menu endpoint to `/menu/all`.

## Task 1: Add Backend Test Project

**Files:**

- Create: `tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`
- Create: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `MiniAdmin.slnx`

- [ ] **Step 1: Create xUnit test project**

Run:

```powershell
dotnet new xunit -n MiniAdmin.Tests -o tests/MiniAdmin.Tests -f net10.0
dotnet sln MiniAdmin.slnx add tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
dotnet add tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj reference src/MiniAdmin.Api/MiniAdmin.Api.csproj
dotnet add tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
```

Expected:

```text
The template "xUnit Test Project" was created successfully.
Project `tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj` added to the solution.
Reference `..\..\src\MiniAdmin.Api\MiniAdmin.Api.csproj` added to the project.
PackageReference for package `Microsoft.AspNetCore.Mvc.Testing` added.
```

- [ ] **Step 2: Make Program visible to WebApplicationFactory**

Append this line to `src/MiniAdmin.Api/Program.cs`:

```csharp
public partial class Program;
```

- [ ] **Step 3: Replace the generated test with failing Vben endpoint tests**

Write `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MiniAdmin.Tests;

public sealed class VbenLoginLoopTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public VbenLoginLoopTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_Returns_AccessToken_In_Vben_Response_Wrapper()
    {
        var response = await _client.PostAsJsonAsync("/auth/login", new
        {
            username = "admin",
            password = "123456"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<ApiEnvelope<LoginData>>();

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal("ok", json.Message);
        Assert.False(string.IsNullOrWhiteSpace(json.Data.AccessToken));
    }

    [Fact]
    public async Task UserInfo_Returns_Roles_And_RealName()
    {
        var json = await _client.GetFromJsonAsync<ApiEnvelope<UserInfoData>>("/user/info");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Equal("Admin", json.Data.RealName);
        Assert.Contains("admin", json.Data.Roles);
    }

    [Fact]
    public async Task AccessCodes_Returns_Permission_Code_Array()
    {
        var json = await _client.GetFromJsonAsync<ApiEnvelope<string[]>>("/auth/codes");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Contains("system:user:query", json.Data);
    }

    [Fact]
    public async Task MenuAll_Returns_Backend_Menu_Routes()
    {
        var json = await _client.GetFromJsonAsync<ApiEnvelope<MenuData[]>>("/menu/all");

        Assert.NotNull(json);
        Assert.Equal(0, json.Code);
        Assert.Contains(json.Data, menu => menu.Name == "Dashboard");
    }

    private sealed record ApiEnvelope<T>(int Code, T Data, string Message);
    private sealed record LoginData(string AccessToken);
    private sealed record UserInfoData(string RealName, string[] Roles);
    private sealed record MenuData(string Name, string Path);
}
```

- [ ] **Step 4: Run tests and verify they fail**

Run:

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
```

Expected:

```text
Failed!  - Failed: 4
```

The expected failure reason is that `/auth/login`, `/user/info`, `/auth/codes`, and `/menu/all` do not exist yet.

## Task 2: Add Unified API Response Wrapper

**Files:**

- Create: `src/MiniAdmin.Shared/ApiResponse.cs`

- [ ] **Step 1: Create response wrapper**

Write `src/MiniAdmin.Shared/ApiResponse.cs`:

```csharp
namespace MiniAdmin.Shared;

public sealed record ApiResponse<T>(int Code, T Data, string Message)
{
    public static ApiResponse<T> Ok(T data) => new(0, data, "ok");
    public static ApiResponse<T> Fail(string message, int code = 1) => new(code, default!, message);
}
```

- [ ] **Step 2: Build**

Run:

```powershell
dotnet build MiniAdmin.slnx
```

Expected:

```text
已成功生成。
    0 个警告
    0 个错误
```

## Task 3: Add Vben Contract DTOs And Interfaces

**Files:**

- Create: `src/MiniAdmin.Application.Contracts/Auth/LoginRequest.cs`
- Create: `src/MiniAdmin.Application.Contracts/Auth/LoginResult.cs`
- Create: `src/MiniAdmin.Application.Contracts/Auth/IAuthAppService.cs`
- Create: `src/MiniAdmin.Application.Contracts/Users/CurrentUserDto.cs`
- Create: `src/MiniAdmin.Application.Contracts/Users/IUserAppService.cs`
- Create: `src/MiniAdmin.Application.Contracts/Menus/VbenMenuDto.cs`
- Create: `src/MiniAdmin.Application.Contracts/Menus/IMenuAppService.cs`

- [ ] **Step 1: Create auth DTOs**

Write `src/MiniAdmin.Application.Contracts/Auth/LoginRequest.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginRequest(string Username, string Password);
```

Write `src/MiniAdmin.Application.Contracts/Auth/LoginResult.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginResult(string AccessToken);
```

Write `src/MiniAdmin.Application.Contracts/Auth/IAuthAppService.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public interface IAuthAppService
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetAccessCodesAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create user DTO and interface**

Write `src/MiniAdmin.Application.Contracts/Users/CurrentUserDto.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Users;

public sealed record CurrentUserDto(string RealName, IReadOnlyList<string> Roles);
```

Write `src/MiniAdmin.Application.Contracts/Users/IUserAppService.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Users;

public interface IUserAppService
{
    Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 3: Create Vben menu DTO and interface**

Write `src/MiniAdmin.Application.Contracts/Menus/VbenMenuDto.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Menus;

public sealed record VbenMenuDto(
    string Name,
    string Path,
    string? Component,
    string? Redirect,
    VbenMenuMetaDto Meta,
    IReadOnlyList<VbenMenuDto> Children);

public sealed record VbenMenuMetaDto(
    string Title,
    string? Icon = null,
    int? Order = null,
    bool? AffixTab = null);
```

Write `src/MiniAdmin.Application.Contracts/Menus/IMenuAppService.cs`:

```csharp
namespace MiniAdmin.Application.Contracts.Menus;

public interface IMenuAppService
{
    Task<IReadOnlyList<VbenMenuDto>> GetAllMenusAsync(CancellationToken cancellationToken = default);
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build MiniAdmin.slnx
```

Expected:

```text
已成功生成。
    0 个警告
    0 个错误
```

## Task 4: Add Fake Application Services

**Files:**

- Create: `src/MiniAdmin.Application/Auth/FakeAuthAppService.cs`
- Create: `src/MiniAdmin.Application/Users/FakeUserAppService.cs`
- Create: `src/MiniAdmin.Application/Menus/FakeMenuAppService.cs`

- [ ] **Step 1: Create fake auth service**

Write `src/MiniAdmin.Application/Auth/FakeAuthAppService.cs`:

```csharp
using MiniAdmin.Application.Contracts.Auth;

namespace MiniAdmin.Application.Auth;

public sealed class FakeAuthAppService : IAuthAppService
{
    public Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Username == "admin" && request.Password == "123456")
        {
            return Task.FromResult<LoginResult?>(new LoginResult("mini-admin-fake-access-token"));
        }

        return Task.FromResult<LoginResult?>(null);
    }

    public Task<IReadOnlyList<string>> GetAccessCodesAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> codes =
        [
            "system:user:query",
            "system:user:add",
            "system:user:edit",
            "system:user:remove",
            "system:role:query",
            "system:menu:query"
        ];

        return Task.FromResult(codes);
    }
}
```

- [ ] **Step 2: Create fake user service**

Write `src/MiniAdmin.Application/Users/FakeUserAppService.cs`:

```csharp
using MiniAdmin.Application.Contracts.Users;

namespace MiniAdmin.Application.Users;

public sealed class FakeUserAppService : IUserAppService
{
    public Task<CurrentUserDto> GetCurrentUserAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new CurrentUserDto("Admin", ["admin"]));
    }
}
```

- [ ] **Step 3: Create fake menu service**

Write `src/MiniAdmin.Application/Menus/FakeMenuAppService.cs`:

```csharp
using MiniAdmin.Application.Contracts.Menus;

namespace MiniAdmin.Application.Menus;

public sealed class FakeMenuAppService : IMenuAppService
{
    public Task<IReadOnlyList<VbenMenuDto>> GetAllMenusAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<VbenMenuDto> menus =
        [
            new VbenMenuDto(
                Name: "Dashboard",
                Path: "/",
                Component: null,
                Redirect: "/analytics",
                Meta: new VbenMenuMetaDto("page.dashboard.title", Order: -1),
                Children:
                [
                    new VbenMenuDto(
                        Name: "Analytics",
                        Path: "/analytics",
                        Component: "/dashboard/analytics/index",
                        Redirect: null,
                        Meta: new VbenMenuMetaDto("page.dashboard.analytics", AffixTab: true),
                        Children: [])
                ])
        ];

        return Task.FromResult(menus);
    }
}
```

- [ ] **Step 4: Build**

Run:

```powershell
dotnet build MiniAdmin.slnx
```

Expected:

```text
已成功生成。
    0 个警告
    0 个错误
```

## Task 5: Expose Vben-Compatible Backend Endpoints

**Files:**

- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] **Step 1: Replace Program.cs with service registration and endpoints**

Write `src/MiniAdmin.Api/Program.cs`:

```csharp
using MiniAdmin.Application.Auth;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.Users;
using MiniAdmin.Application.Menus;
using MiniAdmin.Application.Users;
using MiniAdmin.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("VbenDev", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(origin =>
                origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase));
    });
});

builder.Services.AddScoped<IAuthAppService, FakeAuthAppService>();
builder.Services.AddScoped<IUserAppService, FakeUserAppService>();
builder.Services.AddScoped<IMenuAppService, FakeMenuAppService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("VbenDev");

app.MapGet("/health", () => Results.Ok(new
{
    Application = "MiniAdmin.Api",
    Status = "Healthy",
    Timestamp = DateTimeOffset.UtcNow
}))
.WithName("HealthCheck");

app.MapPost("/auth/login", async (
    LoginRequest request,
    IAuthAppService authAppService,
    CancellationToken cancellationToken) =>
{
    var result = await authAppService.LoginAsync(request, cancellationToken);

    return result is null
        ? Results.Unauthorized()
        : Results.Ok(ApiResponse<LoginResult>.Ok(result));
});

app.MapGet("/user/info", async (
    IUserAppService userAppService,
    CancellationToken cancellationToken) =>
{
    var user = await userAppService.GetCurrentUserAsync(cancellationToken);

    return Results.Ok(ApiResponse<CurrentUserDto>.Ok(user));
});

app.MapGet("/auth/codes", async (
    IAuthAppService authAppService,
    CancellationToken cancellationToken) =>
{
    var codes = await authAppService.GetAccessCodesAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<string>>.Ok(codes));
});

app.MapGet("/menu/all", async (
    IMenuAppService menuAppService,
    CancellationToken cancellationToken) =>
{
    var menus = await menuAppService.GetAllMenusAsync(cancellationToken);

    return Results.Ok(ApiResponse<IReadOnlyList<VbenMenuDto>>.Ok(menus));
});

app.Run();

public partial class Program;
```

- [ ] **Step 2: Run tests and verify they pass**

Run:

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
```

Expected:

```text
Passed!  - Failed: 0
```

- [ ] **Step 3: Run build**

Run:

```powershell
dotnet build MiniAdmin.slnx
```

Expected:

```text
已成功生成。
    0 个警告
    0 个错误
```

## Task 6: Run Backend Manually

**Files:**

- No file changes.

- [ ] **Step 1: Start backend**

Run:

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5320
```

Expected:

```text
Now listening on: http://localhost:5320
Application started.
```

- [ ] **Step 2: Verify login endpoint**

Run in another terminal:

```powershell
Invoke-RestMethod -Uri http://localhost:5320/auth/login -Method Post -ContentType "application/json" -Body '{"username":"admin","password":"123456"}'
```

Expected response shape:

```text
code data                              message
---- ----                              -------
0    @{accessToken=mini-admin-fake...} ok
```

- [ ] **Step 3: Verify Vben required endpoints**

Run:

```powershell
Invoke-RestMethod -Uri http://localhost:5320/user/info
Invoke-RestMethod -Uri http://localhost:5320/auth/codes
Invoke-RestMethod -Uri http://localhost:5320/menu/all
```

Expected:

```text
All three commands return code=0 and non-empty data.
```

## Task 7: Pull Official Vben Admin

**Files:**

- Create directory: `frontend/vue-vben-admin`

- [ ] **Step 1: Clone official Vben repository**

Run from repository root:

```powershell
git clone https://github.com/vbenjs/vue-vben-admin.git frontend/vue-vben-admin
```

Expected:

```text
Cloning into 'frontend/vue-vben-admin'...
```

- [ ] **Step 2: Inspect Ant Design app**

Run:

```powershell
Get-ChildItem -LiteralPath frontend/vue-vben-admin/apps
Get-ChildItem -LiteralPath frontend/vue-vben-admin/apps/web-antd
```

Expected:

```text
web-antd
```

- [ ] **Step 3: Install dependencies**

Run:

```powershell
cd frontend/vue-vben-admin
pnpm install
```

Expected:

```text
Done
```

## Task 8: Configure Official Vben To Call MiniAdmin

**Files:**

- Modify after clone: `frontend/vue-vben-admin/apps/web-antd/vite.config.mts`
- Modify after clone: `frontend/vue-vben-admin/apps/web-antd/src/preferences.ts`
- Inspect after clone: `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts`
- Inspect after clone: `frontend/vue-vben-admin/apps/web-antd/src/api/core/user.ts`
- Inspect after clone: `frontend/vue-vben-admin/apps/web-antd/src/api/core/menu.ts`

- [ ] **Step 1: Configure development proxy**

In `frontend/vue-vben-admin/apps/web-antd/vite.config.mts`, ensure the `/api` proxy target points at MiniAdmin:

```ts
import { defineConfig } from '@vben/vite-config';

export default defineConfig(async () => {
  return {
    vite: {
      server: {
        proxy: {
          '/api': {
            changeOrigin: true,
            rewrite: (path) => path.replace(/^\/api/, ''),
            target: 'http://localhost:5320',
            ws: true,
          },
        },
      },
    },
  };
});
```

- [ ] **Step 2: Configure backend access mode**

In `frontend/vue-vben-admin/apps/web-antd/src/preferences.ts`, ensure:

```ts
import { defineOverridesPreferences } from '@vben/preferences';

export const overridesPreferences = defineOverridesPreferences({
  app: {
    accessMode: 'backend',
  },
});
```

- [ ] **Step 3: Confirm auth endpoints**

In `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts`, confirm login and access code endpoints match:

```ts
export async function loginApi(data: AuthApi.LoginParams) {
  return requestClient.post<AuthApi.LoginResult>('/auth/login', data);
}

export async function getAccessCodesApi() {
  return requestClient.get<string[]>('/auth/codes');
}
```

- [ ] **Step 4: Confirm user endpoint**

In `frontend/vue-vben-admin/apps/web-antd/src/api/core/user.ts`, confirm:

```ts
export async function getUserInfoApi() {
  return requestClient.get<UserInfo>('/user/info');
}
```

- [ ] **Step 5: Confirm or adjust backend menu endpoint**

In `frontend/vue-vben-admin/apps/web-antd/src/api/core/menu.ts`, ensure the backend menu function calls `/menu/all`:

```ts
export async function getAllMenus() {
  return requestClient.get('/menu/all');
}
```

If the official function name differs in the cloned version, keep the existing function name and only change the URL to `/menu/all`.

## Task 9: Run Official Vben Against MiniAdmin

**Files:**

- No backend file changes.
- Frontend config files from Task 8.

- [ ] **Step 1: Start backend**

Run:

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5320
```

Expected:

```text
Now listening on: http://localhost:5320
```

- [ ] **Step 2: Start Vben Ant Design app**

Run:

```powershell
cd frontend/vue-vben-admin
pnpm run dev:antd
```

Expected:

```text
Local: http://localhost:5555/
```

If Vben prints a different local port, use the printed URL.

- [ ] **Step 3: Log in**

Use:

```text
username: admin
password: 123456
```

Expected:

```text
The login succeeds, Vben stores the access token, fetches user info, fetches access codes, fetches backend menus, and enters the dashboard.
```

## Task 10: Document What Was Learned

**Files:**

- Create: `docs/02-official-vben-login-loop.md`

- [ ] **Step 1: Write teaching notes**

Write `docs/02-official-vben-login-loop.md`:

```markdown
# Official Vben Login Loop

This stage connects the official Vben Admin frontend to MiniAdmin with fake backend data.

## Endpoints

- `POST /auth/login` returns an `accessToken`.
- `GET /user/info` returns `realName` and `roles`.
- `GET /auth/codes` returns permission code strings.
- `GET /menu/all` returns backend menu routes.

## Temporary Credentials

- Username: `admin`
- Password: `123456`

## Why Fake Data First

Fake data lets us verify the frontend-backend contract before adding MySQL, JWT, and RBAC tables.

## Next Step

Replace the fake access token with real JWT authentication.
```

- [ ] **Step 2: Final verification**

Run:

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
dotnet build MiniAdmin.slnx
```

Expected:

```text
Passed!  - Failed: 0
已成功生成。
    0 个警告
    0 个错误
```

## Self-Review

Spec coverage:

- Official Vben default login interface is covered by Task 5.
- Vben user info is covered by Task 5.
- Vben access codes are covered by Task 5.
- Backend menu mode is covered by Tasks 5, 8, and 9.
- Fake-data-first learning flow is covered by Tasks 4 and 10.
- Official Vben clone and minimal modification path is covered by Tasks 7 and 8.

Completion marker scan:

- No unfinished markers are intentionally left in this plan.
- The only conditional branch is for official Vben function naming in `menu.ts`; the endpoint URL remains fixed as `/menu/all`.

Type consistency:

- Backend login DTO uses `AccessToken`, which serializes as `accessToken`.
- Backend user DTO uses `RealName` and `Roles`, which serialize as `realName` and `roles`.
- Backend response wrapper uses `Code`, `Data`, and `Message`, which serialize as `code`, `data`, and `message`.
