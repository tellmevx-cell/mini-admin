# Notification Policy Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add notification policy management for workflow events while preserving existing template and in-app notification behavior.

**Architecture:** Introduce a `NotificationPolicy` entity and app service beside existing notification templates. Workflow notification creation checks the policy table before creating `UserNotification`; frontend adds a new policy tab to the existing notification center page.

**Tech Stack:** .NET, EF Core, xUnit, Vue 3, TypeScript, ant-design-vue.

---

### Task 1: Backend Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`
- Create: `tests/MiniAdmin.Tests/NotificationPolicyAppServiceTests.cs`

- [x] Add a failing workflow test proving `WorkflowTask` policy can disable in-app task notifications.
- [x] Add a failing app service test proving policies can be listed and updated.
- [x] Run the targeted tests and confirm they fail because the policy layer is missing.

### Task 2: Domain, DTOs, Repository, App Service

**Files:**
- Create: `src/MiniAdmin.Domain/Entities/NotificationPolicy.cs`
- Modify: `src/MiniAdmin.Application.Contracts/UserNotifications/UserNotificationDtos.cs`
- Create: `src/MiniAdmin.Application/UserNotifications/NotificationPolicyAppService.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfNotificationPolicyRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [x] Add policy DTOs, save request, repository interface, and app service interface.
- [x] Implement validation for event name, category, recipient strategy, and at least one meaningful policy state.
- [x] Implement list/update repository operations.
- [x] Register repository and app service.
- [x] Add list/update endpoints protected by notification permissions.

### Task 3: Workflow Policy Binding

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Before queuing a workflow in-app notification, query the policy by `sourceType`.
- [x] Allow when policy is missing.
- [x] Skip when policy is disabled or in-app channel is disabled.
- [x] Keep duplicate prevention and template rendering unchanged.

### Task 4: Database Compatibility and Seeds

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`

- [x] Create `mini_notification_policies` when missing.
- [x] Seed default workflow policies.
- [x] Add `system:notification:policy:update` permission and assign it to admin when notification center is assigned.

### Task 5: Frontend Policy Tab

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/core/notification.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`

- [x] Add policy API types and list/update calls.
- [x] Add `通知策略` tab with query, table, and edit modal.
- [x] Gate editing by `system:notification:policy:update`.
- [x] Explain email/webhook are reserved toggles.

### Task 6: Verification and Restart

**Files:**
- None

- [x] Run targeted backend tests.
- [x] Run `pnpm run build:antd`.
- [x] Restart backend and frontend.
- [x] Verify `http://localhost:5021/health` and `http://localhost:5666/` return HTTP 200.
