# Permission Diagnostics Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a system page that explains a user's effective roles, permission codes, menu entries, data scope, and lets admins refresh that user's authorization cache.

**Architecture:** Add a backend application service and repository for permission diagnostics. The repository reads users, roles, menus, data scope, and cache settings from existing EF/cache services; the frontend renders a focused diagnostics workspace under system management.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, distributed cache abstraction, Vue/Vben Ant Design.

---

### Task 1: Backend Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add a test that calls `/system/permission-diagnostics/user/{userName}` as admin.
- [ ] Assert the response includes user profile, roles, permission codes, data scope, and cache keys.
- [ ] Add a test that `POST /system/permission-diagnostics/user/{userName}/refresh-cache` succeeds.

### Task 2: Backend Implementation

**Files:**
- Create: `src/MiniAdmin.Application.Contracts/PermissionDiagnostics/*.cs`
- Create: `src/MiniAdmin.Application/PermissionDiagnostics/PermissionDiagnosticsAppService.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfPermissionDiagnosticsRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Add DTOs and interfaces.
- [ ] Resolve effective roles, permission codes, visible menus, data scope, and cache keys.
- [ ] Add query and refresh-cache endpoints protected by `system:permission-diagnostics:query` and `system:permission-diagnostics:refresh-cache`.
- [ ] Seed menu and permission buttons for admin role.

### Task 3: Frontend

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/system/permission-diagnostics.ts`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/views/system/permission-diagnostics/index.vue`

- [ ] Add a searchable diagnostics page.
- [ ] Show user, roles, data scope, permission codes, menu tree, and cache keys.
- [ ] Add refresh-cache action.

### Task 4: Verification

- [ ] Run focused backend tests.
- [ ] Run full backend tests.
- [ ] Run `pnpm run build:antd`.
