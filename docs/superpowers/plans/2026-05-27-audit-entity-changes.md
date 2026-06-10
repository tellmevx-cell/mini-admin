# Entity Change Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Record database before/after snapshots for audited write requests and show those changes in the audit log detail view.

**Architecture:** Add a scoped entity-change collector that is enabled only during audited HTTP requests. `MiniAdminDbContext.SaveChangesAsync` captures EF changes before saving, and `EfAuditLogRepository.CreateAsync` persists the captured changes under the request audit log.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL/InMemory providers, Vue/Vben Ant Design frontend.

---

### Task 1: Backend Behavior Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add a test that updates a user and asserts the audit log contains an entity change for `User` with `Update`, before JSON, after JSON, and field diff JSON.
- [ ] Run the focused test and verify it fails before implementation.

### Task 2: Backend Entity Change Capture

**Files:**
- Create: `src/MiniAdmin.Domain/Entities/AuditEntityChange.cs`
- Create: `src/MiniAdmin.Application.Contracts/AuditLogs/AuditEntityChangeDto.cs`
- Create: `src/MiniAdmin.Application.Contracts/AuditLogs/CapturedAuditEntityChange.cs`
- Create: `src/MiniAdmin.Application.Contracts/AuditLogs/IAuditEntityChangeCollector.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/AuditEntityChangeCollector.cs`
- Modify: `src/MiniAdmin.Domain/Entities/AuditLog.cs`
- Modify: `src/MiniAdmin.Application.Contracts/AuditLogs/AuditLogDto.cs`
- Modify: `src/MiniAdmin.Application.Contracts/AuditLogs/SaveAuditLogRequest.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfAuditLogRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- Modify: `src/MiniAdmin.Api/AuditLogMiddleware.cs`

- [ ] Add the entity/table mapping and MySQL ensure-table SQL.
- [ ] Capture `Added`, `Modified`, and `Deleted` EF entries except audit/log runtime entities.
- [ ] Persist captured changes when request audit log is created.
- [ ] Include entity changes in audit log list DTOs.

### Task 3: Frontend Detail View

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/log/index.vue`

- [ ] Add an entity change section in the existing audit detail modal.
- [ ] Display entity name, entity id, operation type, and before/after/diff JSON.

### Task 4: Verification

- [ ] Run focused backend audit tests.
- [ ] Build the frontend with `pnpm run build:antd`.
