# SaaS Tenant Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first SaaS tenant foundation slice: tenant records, login tenant recognition, JWT tenant claims, current tenant context, and tenant status validation.

**Architecture:** Keep this slice narrow and compatible with the current MiniAdmin layering. The domain layer defines tenant entities and small multi-tenancy contracts, application services resolve tenants during login, infrastructure persists tenants and emits JWT claims, and API validation derives tenant context from the token.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL initializer compatibility SQL, xUnit integration tests, Vben frontend follow-up in later tenant management phase.

---

### Task 1: Tenant Login Contract Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add tests that prove platform users can log in without `tenantCode`, tenant users can log in with a valid `tenantCode`, missing tenant code is rejected for tenant users, and disabled tenants cannot log in.
- [ ] Run the new tests with `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantLogin"` and confirm they fail because tenant support is missing.

### Task 2: Domain And Contract Types

**Files:**
- Create: `src/MiniAdmin.Domain/Entities/Tenant.cs`
- Create: `src/MiniAdmin.Domain/Entities/TenantPackage.cs`
- Create: `src/MiniAdmin.Domain.Shared/MultiTenancy/TenantStatus.cs`
- Create: `src/MiniAdmin.Domain.Shared/MultiTenancy/IHasTenant.cs`
- Create: `src/MiniAdmin.Application.Contracts/MultiTenancy/ICurrentTenant.cs`
- Create: `src/MiniAdmin.Application.Contracts/MultiTenancy/ITenantRepository.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Auth/LoginResult.cs`

- [ ] Define tenant entities with `Id`, `Name`, `Code`, `Status`, package and contact fields.
- [ ] Define `IHasTenant` and `ICurrentTenant` so later entities can share the same isolation contract.
- [ ] Extend login result with nullable `tenantId` and `tenantCode`.

### Task 3: Persistence And Seed Data

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`

- [ ] Map tenant tables in EF.
- [ ] Add a default active demo tenant seed.
- [ ] Add MySQL initializer SQL for `mini_tenants` and `mini_tenant_packages`.
- [ ] Register `ITenantRepository`.

### Task 4: Login Tenant Recognition And JWT Claims

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Auth/LoginRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Auth/ITokenService.cs`
- Modify: `src/MiniAdmin.Application/Auth/AuthAppService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Auth/JwtTokenService.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Add optional `TenantCode` to login request.
- [ ] Resolve tenant during login.
- [ ] Reject tenant users without tenant code and reject disabled or expired tenants.
- [ ] Add `tenant_id` and `tenant_code` claims when present.
- [ ] Return tenant info to frontend login result.

### Task 5: Current Tenant Context And Request Validation

**Files:**
- Create: `src/MiniAdmin.Infrastructure/MultiTenancy/CurrentTenant.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Parse tenant claims during authenticated requests.
- [ ] Re-check tenant status in JWT validation so disabled tenants cannot continue with old tokens.
- [ ] Keep platform users with empty tenant claims working as before.

### Task 6: Documentation And Verification

**Files:**
- Modify: `docs/features/2026-05-29-saas-tenant-foundation/02-tasks.md`
- Create: `docs/features/2026-05-29-saas-tenant-foundation/03-summary.md`

- [ ] Mark completed first-phase tasks.
- [ ] Document implemented behavior, affected files, test results, and next phase.
- [ ] Run `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`.
- [ ] Run `pnpm run build:antd`.
- [ ] Start backend and verify `http://localhost:5320/health`.
