# Tenant Data Isolation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add first-stage SaaS tenant isolation for users, roles, departments, and positions.

**Architecture:** Use the existing JWT-to-`ICurrentTenant` middleware as the tenant source. Add `TenantId` to role, department, and position entities, then apply explicit repository-level filters and creation-time assignment. Keep platform users unrestricted while tenant users are restricted to their own tenant.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL/InMemory, official Vben frontend.

---

### Task 1: Red Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add tests named with `TenantDataIsolation_`.
- [ ] Assert tenant admin cannot see platform users, roles, departments, or positions.
- [ ] Assert tenant-created user is assigned to current tenant.
- [ ] Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantDataIsolation"
```

Expected before implementation: tests fail because roles, departments, and positions are not tenant-aware yet.

### Task 2: Tenant Columns

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/Role.cs`
- Modify: `src/MiniAdmin.Domain/Entities/Department.cs`
- Modify: `src/MiniAdmin.Domain/Entities/Position.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] Add nullable `TenantId` and `Tenant` navigation to Role, Department, Position.
- [ ] Add EF indexes and tenant relationships.
- [ ] Add MySQL compatibility upgrade methods for old databases.

### Task 3: Repository Isolation

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfDepartmentRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfPositionRepository.cs`

- [ ] Inject `ICurrentTenant`.
- [ ] Apply tenant filters in list/read paths.
- [ ] Assign current tenant in create paths.
- [ ] Reject cross-tenant role, department, and position references.

### Task 4: Verification

**Files:**
- Modify: `docs/features/2026-05-29-tenant-data-isolation/02-tasks.md`
- Create: `docs/features/2026-05-29-tenant-data-isolation/03-summary.md`

- [ ] Run tenant isolation tests.
- [ ] Run full backend tests.
- [ ] Run frontend build.
- [ ] Restart backend and verify `/health`.
- [ ] Verify frontend returns 200.
