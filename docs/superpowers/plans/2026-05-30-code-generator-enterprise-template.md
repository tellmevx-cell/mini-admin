# Code Generator Enterprise Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make generated CRUD modules optionally include enterprise data-scope filtering.

**Architecture:** Add data-scope options to the generator request contract, validate them in the app service, and branch the renderer so generated repositories/endpoints include data-scope code only when requested. The frontend exposes the options in the existing generator configuration band.

**Tech Stack:** .NET 10 Minimal API, EF Core, xUnit, Vue 3, Ant Design Vue, official Vben frontend.

---

### Task 1: Backend Contract And Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`

- [ ] Add a failing test named `CodeGeneratorPreview_Includes_DataScope_For_Department_Mode`.
- [ ] Extend `CodeGeneratorPreviewRequest` with `DataScopeMode`, `DataScopeField`, and `EnableAudit` default values.
- [ ] Run `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorPreview_Includes_DataScope_For_Department_Mode"` and confirm the test fails before implementation.

### Task 2: Renderer And Validation

**Files:**
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`

- [ ] Validate that enabled data-scope modes have a selected field.
- [ ] Generate `CurrentUserName` query plumbing for scoped modules.
- [ ] Generate `IDataScopeProvider` repository filtering for list, update, and delete.
- [ ] Add install-plan steps for tenant mode, data scope, and audit coverage.

### Task 3: Frontend Options

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [ ] Add TS fields for `dataScopeMode`, `dataScopeField`, and `enableAudit`.
- [ ] Add data-scope mode and field controls.
- [ ] Clear preview state when those controls change.
- [ ] Show enterprise template hints in the configuration area.

### Task 4: Verification And Summary

**Files:**
- Add: `docs/features/2026-05-30-code-generator-enterprise-template/03-summary.md`

- [ ] Run code generator tests.
- [ ] Run frontend static check and build.
- [ ] Check backend health and frontend HTTP 200 if services are running.
- [ ] Write completion summary.
