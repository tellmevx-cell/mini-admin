# Login Security Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add failure-triggered captcha and temporary lockout to the MiniAdmin login flow.

**Architecture:** The backend owns all security decisions. Captcha codes and failed-attempt counters use the existing distributed cache abstraction, so Redis and memory cache both work. The frontend only renders captcha when the backend says it is required.

**Tech Stack:** ASP.NET Core minimal API, EF Core, `IDistributedCache`, Vben Vue 3 Ant Design, Pinia.

---

### Task 1: Backend Contracts And Tests

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Auth/LoginRequest.cs`
- Create: `src/MiniAdmin.Application.Contracts/Auth/LoginFailureException.cs`
- Create: `src/MiniAdmin.Application.Contracts/Auth/CaptchaDto.cs`
- Create: `src/MiniAdmin.Application.Contracts/Auth/ILoginSecurityService.cs`
- Test: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] **Step 1: Extend login request and add contracts**

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public sealed record LoginRequest(
    string Username,
    string Password,
    string? ClientIp = null,
    string? CaptchaId = null,
    string? CaptchaCode = null);
```

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public sealed record CaptchaDto(string Id, string ImageBase64, int ExpiresInSeconds);
```

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public sealed class LoginFailureException(
    string message,
    bool captchaRequired,
    int? lockRemainingSeconds = null) : Exception(message)
{
    public bool CaptchaRequired { get; } = captchaRequired;

    public int? LockRemainingSeconds { get; } = lockRemainingSeconds;
}
```

```csharp
namespace MiniAdmin.Application.Contracts.Auth;

public interface ILoginSecurityService
{
    Task<CaptchaDto> CreateCaptchaAsync(CancellationToken cancellationToken = default);

    Task ValidateBeforePasswordAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task RecordFailureAsync(string userName, string? clientIp, CancellationToken cancellationToken = default);

    Task ClearFailuresAsync(string userName, string? clientIp, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Add failing tests**

Add tests that call `/auth/login` with wrong passwords three times, assert the fourth response has `captchaRequired = true`, then fail two more times and assert the account/ip pair is locked.

- [ ] **Step 3: Run focused tests to verify failure**

Run:

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminLoginSecurityRed'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "LoginSecurity"
```

Expected: fails because the new security contracts and endpoints are not implemented yet.

### Task 2: Cache-Backed Captcha And Lockout

**Files:**
- Create: `src/MiniAdmin.Infrastructure\Auth\LoginSecurityOptions.cs`
- Create: `src/MiniAdmin.Infrastructure\Auth\DistributedLoginSecurityService.cs`
- Modify: `src/MiniAdmin.Infrastructure\Persistence\MiniAdminPersistenceServiceCollectionExtensions.cs`

- [ ] **Step 1: Implement options**

```csharp
namespace MiniAdmin.Infrastructure.Auth;

public sealed class LoginSecurityOptions
{
    public int CaptchaRequiredFailures { get; set; } = 3;

    public int LockoutFailures { get; set; } = 5;

    public int LockoutMinutes { get; set; } = 10;

    public int CaptchaExpireSeconds { get; set; } = 120;
}
```

- [ ] **Step 2: Implement cache-backed service**

Create a service that stores `login-security:captcha:{id}` and `login-security:failures:{username}:{ip}` in `IDistributedCache`. Captcha image can be SVG rendered as base64 data URL for now, with noisy text lines and four digits.

- [ ] **Step 3: Register service**

Register `LoginSecurityOptions` from configuration and `ILoginSecurityService` as scoped.

- [ ] **Step 4: Run focused tests**

Run the same `LoginSecurity` filter and verify captcha/lockout tests pass.

### Task 3: Auth Flow Integration

**Files:**
- Modify: `src/MiniAdmin.Application\Auth\AuthAppService.cs`
- Modify: `src/MiniAdmin.Api\Program.cs`

- [ ] **Step 1: Inject login security service**

Add `ILoginSecurityService` to `AuthAppService`.

- [ ] **Step 2: Validate before password verification**

Before password verification, call `ValidateBeforePasswordAsync(request)`. On invalid user/password, call `RecordFailureAsync`. On success, call `ClearFailuresAsync`.

- [ ] **Step 3: Expose captcha endpoint**

Add:

```csharp
app.MapGet("/auth/captcha", async (
    ILoginSecurityService loginSecurityService,
    CancellationToken cancellationToken) =>
{
    var captcha = await loginSecurityService.CreateCaptchaAsync(cancellationToken);
    return Results.Ok(ApiResponse<CaptchaDto>.Ok(captcha));
});
```

- [ ] **Step 4: Return structured login failure**

Catch `LoginFailureException` in `/auth/login` and return an envelope with message, `captchaRequired`, and `lockRemainingSeconds`.

### Task 4: Vben Login Page Integration

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/store/auth.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/_core/authentication/login.vue`

- [ ] **Step 1: Add captcha API types**

Add `captchaId`, `captchaCode`, and `getCaptchaApi()`.

- [ ] **Step 2: Surface captcha-required errors**

When login fails with `captchaRequired`, load captcha and show captcha input.

- [ ] **Step 3: Render captcha field**

Add a compact captcha row below password: input on the left, clickable image on the right.

- [ ] **Step 4: Refresh captcha after failed captcha validation**

If backend still returns `captchaRequired`, request a fresh captcha image.

### Task 5: Verification

**Files:**
- Test: `tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`
- Build: `MiniAdmin.slnx`
- Build: `frontend/vue-vben-admin`

- [ ] **Step 1: Run backend tests**

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminLoginSecurityFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

Expected: all tests pass.

- [ ] **Step 2: Build backend**

```powershell
dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx
```

Expected: 0 errors.

- [ ] **Step 3: Build frontend**

```powershell
pnpm run build:antd
```

Expected: Vben Antd build succeeds.

- [ ] **Step 4: Manual test**

Start backend and frontend, fail admin login three times, confirm captcha appears, fail until lockout appears, wait or switch IP/username, then confirm correct login still works after state is cleared.
