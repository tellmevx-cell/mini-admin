# Workflow Attachments And Comments Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add workflow instance attachments and comments so approvals support basic collaboration.

**Architecture:** Reuse existing `ManagedFile` upload/download APIs and store workflow-specific association rows. Keep comments instance-level and notify workflow participants through existing notification templates and deep links.

**Tech Stack:** .NET 10, EF Core, xUnit, Vue 3, ant-design-vue, Vben Admin.

---

### Task 1: Backend Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Add a failing test that starting an instance with attachment IDs returns attachment DTOs.
- [x] Add a failing test that adding a duplicate attachment is rejected or ignored deterministically.
- [x] Add a failing test that adding a comment persists it and writes a workflow action log.
- [x] Add a failing test that adding a comment sends `WorkflowComment` notifications to workflow participants except the author.
- [x] Run targeted tests to confirm red before production code.

### Task 2: Contracts And Entities

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Create: `src/MiniAdmin.Domain/Entities/WorkflowAttachment.cs`
- Create: `src/MiniAdmin.Domain/Entities/WorkflowComment.cs`
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowInstance.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [x] Add attachment/comment DTOs and requests.
- [x] Add service contract methods for add attachment and add comment.
- [x] Add domain entities and navigation collections.
- [x] Configure EF tables, indexes, relationships, and MySQL schema creation/backfill.

### Task 3: Repository And Notifications

**Files:**
- Modify: `src/MiniAdmin.Application/Workflows/WorkflowAppService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [x] Validate attachment IDs and comment content.
- [x] Map attachments/comments into `WorkflowInstanceDto`.
- [x] Attach files during instance start.
- [x] Implement `AddAttachmentAsync` and `AddCommentAsync`.
- [x] Write `Comment` action log and queue `WorkflowComment` notifications.
- [x] Seed `WorkflowComment` notification template.

### Task 4: API Endpoints

**Files:**
- Modify: `src/MiniAdmin.Api/Program.cs`

- [x] Add `POST /workflow/instance/{id}/attachments`.
- [x] Add `POST /workflow/instance/{id}/comments`.
- [x] Apply existing workflow permissions.
- [x] Return standard `ApiResponse` envelopes and workflow operation errors.

### Task 5: Frontend Workflow Center

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add workflow attachment/comment API types and methods.
- [x] Add start-form attachment uploader using existing `uploadFileApi`.
- [x] Include attachment IDs when starting an instance.
- [x] Show attachments in detail drawer with download buttons.
- [x] Add detail drawer comment composer and comment list.
- [x] Reload detail after attachment/comment changes.

### Task 6: Message Center And Verification

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`

- [x] Add `WorkflowComment` source type label/filter.
- [x] Run targeted backend tests.
- [x] Run `pnpm run build:antd`.
- [x] Restart backend/frontend and verify `http://localhost:5021/health` and `http://localhost:5666/`.
