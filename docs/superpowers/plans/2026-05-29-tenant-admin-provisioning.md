# Tenant Admin Provisioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a usable tenant administrator account whenever platform administrators create a new tenant.

**Architecture:** Keep the tenant opening workflow inside the existing tenant application service boundary. The API contract carries administrator fields, `EfTenantRepository` creates tenant, user, and role binding together, and initializer ensures the built-in `tenant-admin` role exists for upgraded databases.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL/InMemory, xUnit integration tests, Vben + Ant Design Vue.

---

### Task 1: Backend Contract Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add a test named `TenantAdmin_CreateTenant_Creates_Admin_And_Allows_Login` that posts `/platform/tenant` with tenant and admin fields, asserts the created user exists with `TenantId`, has `tenant-admin`, and can log in with `tenantCode`.
- [ ] Add a test named `TenantAdmin_CreateTenant_Rejects_Duplicate_Admin_UserName` that creates a tenant with admin username `admin` and expects `400 BadRequest`.
- [ ] Run `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantAdmin"` and verify the tests fail before implementation.

### Task 2: Backend Implementation

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/CreateTenantRequest.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs`

- [ ] Add admin fields to `CreateTenantRequest`.
- [ ] Seed the built-in `tenant-admin` role idempotently.
- [ ] Inject `IPasswordService` into `EfTenantRepository`.
- [ ] Validate tenant code, tenant name, admin username, admin real name, and admin password.
- [ ] Reject duplicate tenant code and duplicate admin username.
- [ ] Create tenant, tenant admin user, and `UserRole` relation in one save flow.
- [ ] Run `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantAdmin"` and verify the tests pass.

### Task 3: Frontend Implementation

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue`

- [ ] Extend `CreateTenantParams` with `adminUserName`, `adminRealName`, `adminEmail`, and `adminPassword`.
- [ ] Add administrator fields to the create modal only.
- [ ] Validate create form requires tenant code, tenant name, admin username, admin real name, and admin password.
- [ ] Keep edit mode focused on tenant fields and do not show password fields.
- [ ] Run `pnpm run build:antd`.

### Task 4: Documentation And Verification

**Files:**
- Modify: `docs/features/2026-05-29-tenant-admin-provisioning/02-tasks.md`
- Create: `docs/features/2026-05-29-tenant-admin-provisioning/03-summary.md`

- [ ] Mark task checklist.
- [ ] Document behavior, affected files, verification results, and next recommendation.
- [ ] Run `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`.
- [ ] Run `pnpm run build:antd`.
- [ ] Start backend at `http://localhost:5320` and verify `/health`.
- [ ] Verify frontend at `http://localhost:5666/`.
