# Workflow Visibility Permissions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restrict workflow details, collaboration actions, and workflow attachment downloads to participants or workflow managers.

**Architecture:** Keep the existing workflow repository as the enforcement point. Extend `WorkflowUserContext` with a management flag sourced from JWT permissions at the API boundary, then reuse one participant predicate for list/detail/comment/attachment/download checks.

**Tech Stack:** .NET 10, EF Core, xUnit, Vue 3, ant-design-vue, Vben Admin.

---

### Task 1: Backend Red Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Add a failing test that a non-participant receives `null` from `GetInstanceAsync`.
- [x] Add a failing test that regular `scope=all` lists only participated instances.
- [x] Add a failing test that a manager context can see unrelated instances.
- [x] Add failing tests that non-participants cannot add comments or attachments.
- [x] Add failing tests for participant and non-participant workflow attachment downloads.
- [x] Run `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests" --no-restore` and confirm the new tests fail for missing enforcement.

### Task 2: Workflow Contracts

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Application/Workflows/WorkflowAppService.cs`

- [x] Extend `WorkflowUserContext` with `bool CanManageAllWorkflowInstances = false`.
- [x] Add `GetAttachmentDownloadAsync(Guid instanceId, Guid attachmentId, WorkflowUserContext user, CancellationToken cancellationToken = default)` to `IWorkflowAppService`.
- [x] Validate empty IDs in `WorkflowAppService.GetAttachmentDownloadAsync`.

### Task 3: Repository Access Checks

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Add `CanAccessInstance(WorkflowInstance instance, WorkflowUserContext user)`.
- [x] Filter regular `GetInstancesAsync(scope=all)` to initiator, task approver, or copied user.
- [x] Apply access check in `GetInstanceAsync`, `AddAttachmentAsync`, and `AddCommentAsync`.
- [x] Implement `GetAttachmentDownloadAsync` by loading the instance with attachments and returning the associated file ID only after access passes.

### Task 4: API And Frontend

**Files:**
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Map `workflow:definition:manage` JWT permission claims into `WorkflowUserContext.CanManageAllWorkflowInstances`.
- [x] Add `GET /workflow/instance/{id}/attachments/{attachmentId}/download`.
- [x] Add frontend `downloadWorkflowAttachmentApi(instanceId, attachmentId)`.
- [x] Update detail drawer download buttons to call the workflow-scoped download endpoint.
- [x] Rename scope options to distinguish regular participated view and manager all view.

### Task 5: Verification

**Files:**
- Modify: `docs/superpowers/plans/2026-06-09-workflow-visibility-permissions.md`

- [x] Run targeted workflow tests.
- [x] Run `pnpm run build:antd`.
- [x] Restart backend/frontend if needed.
- [x] Verify `http://localhost:5021/health` and `http://localhost:5666/`.
