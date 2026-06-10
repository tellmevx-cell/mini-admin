# Common Import Export Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add reusable Excel import/export, prove it on position management, and make the code generator emit import/export-enabled CRUD modules.

**Architecture:** Keep xlsx parsing in a common infrastructure service, keep module-specific validation in each app service, and keep tenant-aware persistence in repositories. The first concrete module is position management; the generator then copies the same contract and UI pattern into generated modules when enabled.

**Tech Stack:** .NET 10 Minimal API, EF Core, lightweight OpenXML xlsx via `ZipArchive`/`XDocument`, Vben Vue, Ant Design Vue.

---

### Task 1: Position Import Export Contract And Failing Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Create: `src/MiniAdmin.Application.Contracts/Common/IWorkbookService.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Positions/IPositionAppService.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Positions/IPositionRepository.cs`
- Create: `src/MiniAdmin.Application.Contracts/Positions/PositionImportDtos.cs`

- [ ] Add integration tests for position template, preview errors, import success and export workbook.
- [ ] Run `dotnet test ... --filter "PositionImportExport"` and verify failure from missing endpoints.
- [ ] Add DTO and interface contracts.

### Task 2: Common Workbook Service And Position Backend

**Files:**
- Create: `src/MiniAdmin.Infrastructure/Common/XlsxWorkbookService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Users/XlsxUserImportExportService.cs`
- Modify: `src/MiniAdmin.Infrastructure/DependencyInjection.cs`
- Modify: `src/MiniAdmin.Application/Positions/PositionAppService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfPositionRepository.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] Move xlsx read/write to shared service.
- [ ] Implement position row parsing and validation.
- [ ] Implement export and import endpoints.
- [ ] Seed `system:position:import` and `system:position:export`.
- [ ] Run position import/export tests and make them pass.

### Task 3: Position Frontend

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/position.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/position/index.vue`

- [ ] Add API helpers for export/template/import/preview/error-report.
- [ ] Add toolbar buttons and hidden file input.
- [ ] Add import preview modal with summary and error table.
- [ ] Run `npx impeccable` on the position page.

### Task 4: Code Generator Integration

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add `EnableImportExport` to preview request.
- [ ] Add import/export permissions to generated menu install plan.
- [ ] Render generated backend and frontend import/export code when enabled.
- [ ] Add generator test that preview output contains import/export endpoints and frontend buttons.

### Task 5: Verification And Docs

**Files:**
- Create: `docs/features/2026-05-30-common-import-export/03-summary.md`

- [ ] Run `dotnet test ... --filter "PositionImportExport|CodeGenerator"`.
- [ ] Run impeccable on position and code generator pages.
- [ ] Run `pnpm run build:antd`.
- [ ] Restart backend and frontend.
- [ ] Write summary document with verification results.

