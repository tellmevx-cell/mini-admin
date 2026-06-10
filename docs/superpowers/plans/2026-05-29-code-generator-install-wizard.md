# Code Generator Install Wizard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an installation readiness guide to code generator preview so developers know file conflicts, table readiness, missing-table SQL, and next steps before generating code.

**Architecture:** Reuse the existing preview endpoint and attach an `InstallPlan` object. Backend owns table existence checks and SQL draft generation; frontend renders the returned plan next to file preview without making a second request.

**Tech Stack:** ASP.NET Core Minimal API, EF Core/MySQL, xUnit, Vue 3, Ant Design Vue, official Vben.

---

### Task 1: Backend Install Plan Contract

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Add DTOs for install plan and install steps.
- [ ] Extend preview result with `InstallPlan`.
- [ ] Add test records matching the response shape.

### Task 2: Table Existence and SQL Draft

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`

- [ ] Add `TableExistsAsync`.
- [ ] Implement MySQL information schema lookup.
- [ ] Make preview async and build install steps.
- [ ] Generate MySQL `CREATE TABLE` draft when the table is missing.

### Task 3: Frontend Install Panel

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [ ] Extend frontend API interfaces.
- [ ] Store returned install plan on preview.
- [ ] Clear install plan when table/config changes.
- [ ] Render readiness steps and SQL draft.

### Task 4: Verification

**Files:**
- Modify: `docs/features/2026-05-29-code-generator-install-wizard/03-summary.md`

- [ ] Run targeted backend test for install plan.
- [ ] Run code generator test group.
- [ ] Run Vben build.
- [ ] Update summary with verification evidence.
