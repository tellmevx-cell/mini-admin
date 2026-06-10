# Permission Diagnostics Chain Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enhance permission diagnostics so admins can see whether missing menus come from user status, role grants, tenant package limits, final intersection, or cache.

**Architecture:** Extend the existing diagnostics endpoint and page instead of adding a new menu. The repository computes role menu contributions, tenant package limits, final menu counts, and human-readable warnings.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, xUnit, Vue 3, Ant Design Vue, Vben.

---

### Task 1: Backend Contract And Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `src/MiniAdmin.Application.Contracts/PermissionDiagnostics/PermissionDiagnosticsDto.cs`

- [ ] Add a failing test that creates a tenant user with a role, calls `/system/permission-diagnostics/user/{userName}`, and asserts tenant package, role contribution, and warnings are returned.
- [ ] Extend diagnostics DTO with `Tenant`, `Effective`, and `Warnings`.
- [ ] Run the filtered test and confirm it fails before repository implementation.

### Task 2: Repository Aggregation

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfPermissionDiagnosticsRepository.cs`

- [ ] Query role menu IDs per role.
- [ ] Query tenant package menu IDs when the user belongs to a tenant.
- [ ] Compute final menu IDs as role menu IDs intersected with package menu IDs for tenant users.
- [ ] Generate warnings for disabled user, no active roles, no role menus, empty package, and empty intersection.
- [ ] Return visible menu count, button permission count, permission code count, and cache keys.

### Task 3: Frontend Page

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/permission-diagnostics.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/permission-diagnostics/index.vue`

- [ ] Add TypeScript interfaces for tenant summary, effective summary, role menu counts, and warnings.
- [ ] Add top summary cards for diagnosis, tenant package, effective result, and cache.
- [ ] Update role section to show menu/button counts per role.
- [ ] Keep existing menu and permission code tables.

### Task 4: Verification

**Files:**
- Modify: `docs/features/2026-05-29-permission-diagnostics-chain/03-summary.md`

- [ ] Run filtered diagnostics tests.
- [ ] Run full backend tests.
- [ ] Run frontend build.
- [ ] Restart backend and frontend.
- [ ] Verify `/system/permission-diagnostics/user/liqing` returns tenant/package and warnings data.
