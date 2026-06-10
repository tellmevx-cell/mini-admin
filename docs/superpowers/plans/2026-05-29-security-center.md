# Security Center Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first enterprise security center slice: overview metrics, security events, final-admin protection, and session invalidation for risky account changes.

**Architecture:** Add a focused `Security` application area backed by a `SecurityEvent` entity and EF repository. Reuse login logs, online users, audit logs, and user/role/menu data for aggregation, and expose minimal API endpoints consumed by a Vben page under system monitoring.

**Tech Stack:** .NET 10 minimal APIs, EF Core MySQL/InMemory, xUnit integration tests, Vue Vben Admin with Ant Design Vue.

---

### Task 1: Backend Contracts And Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Create: `src/MiniAdmin.Domain/Entities/SecurityEvent.cs`
- Create: `src/MiniAdmin.Application.Contracts/Security/SecurityDtos.cs`

- [ ] Write failing integration tests for:
  - `GET /system/security-center/overview` returns account/login/permission/session metrics.
  - `GET /system/security-event/list` returns recent security events.
  - deleting or disabling the last admin user returns `400`.
  - disabling a user invalidates the old token.
- [ ] Run focused tests and confirm they fail because endpoints/types do not exist.

### Task 2: Security Event Persistence

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- Create: `src/MiniAdmin.Application.Contracts/Security/ISecurityCenterAppService.cs`
- Create: `src/MiniAdmin.Application/Security/SecurityCenterAppService.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfSecurityEventRepository.cs`

- [ ] Add `SecurityEvent` EF mapping and MySQL compatibility table.
- [ ] Implement repository methods for recording, listing, and aggregating security events.
- [ ] Register repository and app service in DI.
- [ ] Run focused tests and continue to expected endpoint failures.

### Task 3: API, Menu, And Event Sources

**Files:**
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: user, role, menu, and online-user repositories as needed.

- [ ] Add `/system/security-center/overview` and `/system/security-event/list`.
- [ ] Seed security center menu and permissions under system monitor.
- [ ] Record security events for login failure, account lock/unlock, force logout, user disabled, role authorization changes, user role changes, and menu permission changes.
- [ ] Enforce final-admin delete/disable protection in backend user operations.
- [ ] Invalidate disabled users by changing security stamp and marking them offline.
- [ ] Run focused backend tests and confirm they pass.

### Task 4: Frontend Security Center Page

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/system/security-center.ts`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/views/system/security-center/index.vue`
- Modify route/menu typing only if needed by existing Vben conventions.

- [ ] Add typed API client.
- [ ] Build a compact operational dashboard: metrics, recent events, risky operations, locked users, stale users, forced logouts.
- [ ] Keep visual style aligned with current system monitor pages.
- [ ] Run `pnpm run build:antd`.

### Task 5: Documentation, Verification, Startup

**Files:**
- Modify: `docs/features/2026-05-29-security-center/02-tasks.md`
- Modify: `docs/features/2026-05-29-security-center/03-summary.md`

- [ ] Update task checkboxes and final summary with exact verification results.
- [ ] Run `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`.
- [ ] Run `pnpm run build:antd`.
- [ ] Start backend on `http://localhost:5320` and frontend on `http://localhost:5666`.
- [ ] Confirm `/health` and frontend root respond successfully.
