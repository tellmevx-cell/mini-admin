# Workflow Cc Receipts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show read/unread cc receipt records inside the workflow instance detail drawer.

**Architecture:** Reuse the existing `WorkflowCcRecord` model and include cc records in `WorkflowInstanceDto`. The frontend consumes the detail payload and renders a compact receipt list next to approval tasks.

**Tech Stack:** .NET 10, EF Core, Vue 3, ant-design-vue, existing MiniAdmin workflow APIs.

---

### Task 1: Backend Contract And Test

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`
- Test: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Write a failing test named `Instance_Detail_Includes_Cc_Receipt_Records`.
- [x] Assert that the workflow detail payload contains the cc recipient, node name, unread state, and then read state after marking the cc record read.
- [x] Add `IReadOnlyList<WorkflowCcRecordDto> CcRecords` to `WorkflowInstanceDto`.
- [x] Map `instance.CcRecords.OrderBy(x => x.CreatedAt).Select(ToCcRecordDto)` in `ToInstanceDto`.
- [x] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter Instance_Detail_Includes_Cc_Receipt_Records`.

### Task 2: Frontend Detail Drawer

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add `ccRecords: WorkflowCcRecordItem[]` to `WorkflowInstanceItem`.
- [x] Render an `抄送回执` section in the workflow detail drawer.
- [x] Show recipient, source node, read/unread tag, copied time, and read time.
- [x] Reuse the existing `readStatusMeta` and `formatTime` helpers.
- [x] Run `pnpm -F @vben/web-antd run build`.

### Task 3: Verification

**Files:**
- Modify: `docs/features/2026-06-03-message-notification-hub/03-summary.md`

- [x] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter FullyQualifiedName~WorkflowAppServiceTests`.
- [x] Run notification tests if workflow notifications are touched.
- [x] Record verification results and manual test points.

## Verification Results

- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter Instance_Detail_Includes_Cc_Receipt_Records`
  - Passed: 1/1
- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter FullyQualifiedName~WorkflowAppServiceTests`
  - Passed: 47/47
- `pnpm -F @vben/web-antd run build`
  - Passed, generated `apps/web-antd/dist.zip`
