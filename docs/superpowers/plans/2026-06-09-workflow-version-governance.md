# Workflow Version Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make published workflow definitions immutable and preserve launch-time definition version data on workflow instances.

**Architecture:** Keep the existing workflow application service and EF repository boundaries. Add immutable-save validation in the repository, add version snapshot fields to `WorkflowInstance`, expose them through DTO/API, and render the version in the existing workflow center drawer.

**Tech Stack:** .NET, EF Core, xUnit, Vue 3, TypeScript, ant-design-vue.

---

### Task 1: Backend Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Add a failing test proving a published definition cannot be updated before instances exist.
- [x] Add a failing test proving a started instance stores definition code, version, and snapshot JSON.
- [x] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter WorkflowAppServiceTests --no-restore` and confirm the new tests fail for the expected missing behavior.

### Task 2: Backend Model and Repository

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowInstance.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Add `DefinitionCode`, `DefinitionVersion`, and `DefinitionSnapshotJson` to `WorkflowInstance`.
- [x] Add the same launch-time fields to `WorkflowInstanceDto`.
- [x] Reject `UpdateDefinitionAsync` when the definition status is not `Draft`.
- [x] Build and store a compact definition snapshot during `StartInstanceAsync`.
- [x] Include launch-time version fields in `ToInstanceDto`.
- [x] Run the workflow tests and confirm they pass.

### Task 3: Database Compatibility

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [x] Configure EF column shape for the new workflow instance fields.
- [x] Add MySQL startup compatibility checks for the three new columns.
- [x] Backfill existing instance version fields from linked workflow definitions.

### Task 4: Frontend Display and Guardrails

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add version snapshot fields to `WorkflowInstanceItem`.
- [x] Show `流程 v版本` in instance detail and instance lists where useful.
- [x] Disable direct draft saving for published or archived definitions and show a “创建新版本” path.
- [x] Run `pnpm run build:antd` from `frontend/vue-vben-admin`.

### Task 5: Restart and Smoke Test

**Files:**
- None

- [x] Stop any old backend/frontend processes holding ports.
- [x] Start backend on `http://localhost:5021`.
- [x] Start frontend on `http://localhost:5666`.
- [x] Verify both endpoints return HTTP 200.

## Verification Results

- `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Published_Definition_Cannot_Be_Updated_Directly_Even_Without_Instance|Started_Instance_Stores_Definition_Version_Snapshot" --no-restore`
  - Passed: 2/2
- `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests" --no-restore`
  - Passed: 47/47
- `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationTemplateAppServiceTests|SampleOrderWorkflowBindingTests" --no-restore`
  - Passed: 6/6
- `pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false`
  - Passed.
- `pnpm -F @vben/web-antd build`
  - Passed and regenerated `apps/web-antd/dist.zip`.
- `Invoke-WebRequest -Uri http://localhost:5021/health -UseBasicParsing`
  - Returned HTTP 200 with `Healthy`.
- `Invoke-WebRequest -Uri http://localhost:5666/ -UseBasicParsing`
  - Returned HTTP 200.
