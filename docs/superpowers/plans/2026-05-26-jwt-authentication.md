# JWT Authentication Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the fake access token with a real JWT and protect Vben post-login endpoints.

**Architecture:** Keep login public, then require Bearer authentication for `/user/info`, `/auth/codes`, and `/menu/all`. Token creation lives behind an application contract so the API layer owns ASP.NET authentication wiring while the application layer owns the login use case.

**Tech Stack:** .NET 10, ASP.NET Core JWT Bearer authentication, xUnit integration tests, official Vben Admin using Authorization Bearer tokens.

---

## Files

- Modify `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`: assert JWT shape and protected endpoint behavior.
- Modify `src/MiniAdmin.Api/MiniAdmin.Api.csproj`: add JWT bearer package.
- Modify `src/MiniAdmin.Api/appsettings.json`: add local JWT settings with a development signing key.
- Create `src/MiniAdmin.Application.Contracts/Auth/ITokenService.cs`: token issuing abstraction.
- Create `src/MiniAdmin.Application/Auth/JwtTokenService.cs`: JWT token issuer.
- Modify `src/MiniAdmin.Application/Auth/FakeAuthAppService.cs`: issue JWT through `ITokenService`.
- Modify `src/MiniAdmin.Api/Program.cs`: configure JWT bearer, register token service, require auth on protected endpoints.
- Modify `docs/02-official-vben-login-loop.md`: explain the JWT upgrade.

## Task 1: Write Failing JWT Tests

- [ ] Update `VbenLoginLoopTests` so login expects a three-part JWT.
- [ ] Add tests proving `/user/info`, `/auth/codes`, and `/menu/all` return `401 Unauthorized` without a token.
- [ ] Add tests proving those endpoints work with `Authorization: Bearer <token>`.
- [ ] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`.
- [ ] Expected: tests fail because endpoints are not protected and login still returns `mini-admin-fake-access-token`.

## Task 2: Add JWT Infrastructure

- [ ] Add `Microsoft.AspNetCore.Authentication.JwtBearer` to `MiniAdmin.Api`.
- [ ] Add `Jwt` settings to `appsettings.json`.
- [ ] Create `ITokenService`.
- [ ] Create `JwtTokenService`.
- [ ] Register JWT authentication in `Program.cs`.
- [ ] Register `ITokenService`.

## Task 3: Protect Vben Post-Login Endpoints

- [ ] Keep `/auth/login` public.
- [ ] Add `.RequireAuthorization()` to `/user/info`.
- [ ] Add `.RequireAuthorization()` to `/auth/codes`.
- [ ] Add `.RequireAuthorization()` to `/menu/all`.
- [ ] Ensure `app.UseAuthentication()` runs before `app.UseAuthorization()`.

## Task 4: Verify

- [ ] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj`.
- [ ] Run `dotnet build MiniAdmin.slnx`.
- [ ] Verify Vben can still log in because it already sends `Authorization: Bearer <token>`.

## Task 5: Document

- [ ] Update `docs/02-official-vben-login-loop.md` with the JWT behavior.
- [ ] Include the teaching point: fake data can remain, but the access token is now real.

## Self-Review

- The plan starts with failing tests.
- Protected endpoint behavior is explicit.
- Vben compatibility is preserved.
- No MySQL work is included in this phase.
