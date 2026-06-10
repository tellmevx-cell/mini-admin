# Workflow Approval Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let workflow approve nodes choose Any approver or All approvers at runtime.

**Architecture:** Store approval mode on `WorkflowNode`, expose it through workflow DTOs, and branch task completion logic in `EfWorkflowRepository.CompleteTaskAsync`. Frontend keeps the same workflow designer and adds one select in the node property panel.

**Tech Stack:** .NET 10, EF Core, xUnit, Vue 3, ant-design-vue, Vben Admin.

---

### Task 1: Backend Contract And Entity

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowNode.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Add `ApprovalMode` to `WorkflowNode` with default `Any`.
- [x] Add `ApprovalMode` to `WorkflowNodeDto`.
- [x] Add `ApprovalMode` to `SaveWorkflowNodeRequest` with default `Any`.
- [x] Persist and map the field in create, update, copy-version, and DTO mapping.
- [x] Configure column max length and default value in EF.

### Task 2: Runtime Strategy Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Add a test that role node with `Any` closes sibling pending tasks after one approval.
- [x] Add a test that role node with `All` stays pending after first approval.
- [x] Add a test that role node with `All` moves forward after all approvers approve.
- [x] Add a test that role node with `All` rejects immediately when one approver rejects.
- [x] Run the targeted tests and verify they fail before production implementation.

### Task 3: Runtime Implementation

**Files:**
- Modify: `src/MiniAdmin.Application/Workflows/WorkflowAppService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Validate approval mode is `Any` or `All`.
- [x] Keep `Any` behavior compatible with current sibling closure logic.
- [x] For `All`, close siblings only on rejection; on approval, wait until all same-node tasks are no longer pending.
- [x] Queue approval result notification only when the node actually completes.
- [x] Run workflow tests until green.

### Task 4: Frontend Node Configuration

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add `approvalMode` to TypeScript workflow node types.
- [x] Add approval mode options.
- [x] Default new approve nodes to `Any`.
- [x] Preserve `approvalMode` when editing existing definitions.
- [x] Add approval mode select in approve node properties.
- [x] Send `approvalMode` in save payload.

### Task 5: Verification And Restart

**Files:**
- No source edits expected.

- [x] Run `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests" --no-restore`.
- [x] Run `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationTemplateAppServiceTests|WorkflowAppServiceTests|SampleOrderWorkflowBindingTests" --no-restore`.
- [x] Run `pnpm run build:antd` from `frontend/vue-vben-admin`.
- [x] Restart backend on `http://localhost:5021` and frontend on `http://localhost:5666`.
- [x] Check backend `/health` and frontend `/` return HTTP 200.

## Verification Results

- `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Any_Approval_Mode_Closes_Sibling_Tasks_After_First_Approval|All_Approval_Mode_Waits_For_All_Approvers_Before_Moving_Forward|All_Approval_Mode_Rejects_Instance_When_Any_Approver_Rejects" --no-restore`
  - Passed: 3/3
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
