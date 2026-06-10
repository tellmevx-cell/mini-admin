# Workflow SLA Auto Reminder Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add first-version workflow SLA support with task deadlines, overdue display, and automatic reminder notifications.

**Architecture:** Store optional SLA minutes on workflow nodes, calculate task due time when pending tasks are created, and use the existing scheduled-job worker to scan overdue pending tasks. Reuse existing workflow action logs and user notifications instead of introducing a new SLA event table.

**Tech Stack:** .NET 10, EF Core, xUnit, Vue 3, ant-design-vue, Vben Admin.

---

### Task 1: Backend Contract And Storage

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowNode.cs`
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowTask.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Add `SlaMinutes` to `WorkflowNode`.
- [x] Add `DueAt` and `LastAutoRemindedAt` to `WorkflowTask`.
- [x] Add `SlaMinutes` to `WorkflowNodeDto` and `SaveWorkflowNodeRequest`.
- [x] Add `DueAt`, `LastAutoRemindedAt`, and `IsOverdue` to `WorkflowTaskDto`.
- [x] Configure EF columns and MySQL backfill logic.
- [x] Map fields in definition create/update/version copy and task DTO conversion.

### Task 2: Backend Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`
- Create or modify: `tests/MiniAdmin.Tests/ScheduledJobServiceTests.cs` if a focused scheduled-job test file already exists, otherwise keep tests in `WorkflowAppServiceTests`.

- [x] Write a failing test that rejects negative node SLA minutes.
- [x] Write a failing test that creates a pending task with `DueAt`.
- [x] Write a failing test that `workflow-sla-scan` creates one overdue notification and action log.
- [x] Write a failing test that a second scan does not duplicate the same reminder immediately.
- [x] Run targeted tests to confirm red before implementation.

### Task 3: SLA Scanner And Scheduled Job

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [x] Add `ScanOverdueTasksAsync(DateTimeOffset now, CancellationToken)` to workflow repository/service contracts.
- [x] Implement scanner query for pending tasks where `DueAt <= now`.
- [x] Create `WorkflowOverdue` action log and notification.
- [x] Set `LastAutoRemindedAt = now`.
- [x] Return counts and scheduled-job details.
- [x] Seed `workflow-sla-scan` scheduled job and `WorkflowOverdue` notification template.
- [x] Register executor branch for `workflow-sla-scan`.

### Task 4: Frontend Workflow Center

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add SLA and deadline fields to TypeScript workflow types.
- [x] Add node property input "处理时限（分钟）".
- [x] Include `slaMinutes` in definition save payload.
- [x] Show deadline column in todo/done task tables.
- [x] Show overdue tag and deadline in workflow detail task list.
- [x] Keep empty SLA nodes compatible.

### Task 5: Frontend Message Center

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`

- [x] Add `WorkflowOverdue` to message source type filters.
- [x] Ensure the existing notification list can open the workflow deep link unchanged.

### Task 6: Verification And Restart

**Files:**
- No source edits expected.

- [x] Run `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests|NotificationTemplateAppServiceTests|SampleOrderWorkflowBindingTests|Scheduled" --no-restore`.
- [x] Run `pnpm run build:antd` from `frontend/vue-vben-admin`.
- [x] Restart backend on `http://localhost:5021` and frontend on `http://localhost:5666`.
- [x] Verify backend `/health` and frontend `/` return HTTP 200.
