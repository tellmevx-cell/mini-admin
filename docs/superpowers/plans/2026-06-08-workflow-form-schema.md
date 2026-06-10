# Workflow Form Schema Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add configurable workflow form fields and dynamic workflow start forms.

**Architecture:** Store a compact JSON schema on workflow definitions and keep using `FormDataJson` as the runtime payload. Backend validates schema and submitted payload; frontend provides a simple table editor and renders controls dynamically.

**Tech Stack:** .NET 10, EF Core, xUnit, Vue 3, ant-design-vue, Vben Admin.

---

### Task 1: Backend Contract And Storage

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/WorkflowDefinition.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Workflows/WorkflowDtos.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`

- [x] Add `FormSchemaJson` to `WorkflowDefinition`.
- [x] Add `FormSchemaJson` to `WorkflowDefinitionDto` and `SaveWorkflowDefinitionRequest`.
- [x] Configure EF column as long text.
- [x] Include `FormSchemaJson` in create, update, version copy, and DTO mapping.
- [x] Add MySQL initialization/backfill logic for existing databases.

### Task 2: Backend Validation Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`

- [x] Write a test that saves and returns form schema JSON.
- [x] Write a test that rejects duplicate form field codes.
- [x] Write a test that rejects starting a schema-backed workflow without a required field.
- [x] Write a test that accepts a valid schema-backed workflow and stores `FormDataJson`.
- [x] Run targeted tests and verify they fail before implementation.

### Task 3: Backend Validation Implementation

**Files:**
- Modify: `src/MiniAdmin.Application/Workflows/WorkflowAppService.cs`

- [x] Parse and normalize schema on definition save.
- [x] Validate field labels, codes, component types, uniqueness, and select options.
- [x] Validate start payload against required fields, number fields, and select options.
- [x] Keep schema-less definitions compatible with manual JSON.
- [x] Run workflow tests until green.

### Task 4: Frontend API And Definition Editor

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/workflow/center.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Add `formSchemaJson` to TypeScript types.
- [x] Add form field state to definition form.
- [x] Render a field configuration table in the definition tab.
- [x] Support add/remove fields and select option editing.
- [x] Serialize field config to `formSchemaJson` on save.
- [x] Load existing `formSchemaJson` into the editor.

### Task 5: Frontend Dynamic Start Form And Detail

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`

- [x] Parse selected definition schema.
- [x] Render dynamic fields in start approval tab when schema exists.
- [x] Reset dynamic values from defaults when definition changes.
- [x] Serialize dynamic form values to JSON on submit.
- [x] Show form data as a readable field list in workflow detail.
- [x] Keep JSON fallback/preview.

### Task 6: Verification And Restart

**Files:**
- No source edits expected.

- [x] Run `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests" --no-restore`.
- [x] Run `dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationTemplateAppServiceTests|WorkflowAppServiceTests|SampleOrderWorkflowBindingTests" --no-restore`.
- [x] Run `pnpm run build:antd` from `frontend/vue-vben-admin`.
- [x] Restart backend on `http://localhost:5021` and frontend on `http://localhost:5666`.
- [x] Verify backend `/health` and frontend `/` return HTTP 200.
