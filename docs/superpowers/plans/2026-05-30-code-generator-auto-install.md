# Code Generator Auto Install Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add generate-time auto installation for generated module table metadata, menu permissions, Admin role assignment, and cache refresh.

**Architecture:** Keep orchestration in `CodeGeneratorAppService` and database work in `ICodeGeneratorRepository`. Reuse deterministic menu IDs from the template renderer so immediate install and generated seed classes are idempotent.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core, MySQL, Vben Vue, Ant Design Vue.

---

### Task 1: Contract And Failing Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`

- [ ] Add a failing test that posts `/system/code-generator/generate` with `autoInstall = true` and asserts generated menu permission codes and Admin role mappings exist.
- [ ] Add a failing test that posts with `autoInstall = false` and asserts no generated menu permission rows exist.
- [ ] Add `AutoInstall` to `CodeGeneratorGenerateRequest`.
- [ ] Add repository methods for generated menu install status and auto install.

### Task 2: Backend Auto Install

**Files:**
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`

- [ ] Expose deterministic GUID helper from the renderer.
- [ ] Build a generated menu installation model in the app service.
- [ ] Execute create-table SQL only for MySQL relational providers when the table is missing.
- [ ] Upsert menu and permission rows.
- [ ] Grant generated rows to Admin role.
- [ ] Clear Admin authorization cache.
- [ ] Rebuild install plan with `auto-install` and `menu-permissions` steps.

### Task 3: Frontend Toggle

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [ ] Add `autoInstall` parameter to `generateCodeApi`.
- [ ] Add a default-on switch to the generator action area.
- [ ] Update installation copy and success message.

### Task 4: Verification And Summary

**Files:**
- Create: `docs/features/2026-05-30-code-generator-auto-install/03-summary.md`

- [ ] Run backend code generator tests.
- [ ] Run frontend page check.
- [ ] Run frontend build.
- [ ] Start backend and frontend for user verification.
- [ ] Write feature summary with changed files, behavior, and test evidence.
