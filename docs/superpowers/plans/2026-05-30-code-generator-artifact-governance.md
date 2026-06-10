# Code Generator Artifact Governance Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add safe rollback for generated code files and generated menu permissions, with an explicit optional path for deleting generated business tables and data.

**Architecture:** Keep rollback orchestration in `CodeGeneratorAppService`; keep database status/menu cleanup and optional MySQL table deletion in `ICodeGeneratorRepository`; expose one Minimal API endpoint protected by rollback/generate permission.

**Tech Stack:** .NET 10, ASP.NET Core Minimal API, EF Core, Vben Vue, Ant Design Vue.

---

### Task 1: Backend Contract And Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorAppService.cs`
- Modify: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`

- [ ] Add a failing integration test that generates a module, verifies a generated file and generated menu permission exist, calls rollback, then verifies file/menu removal and `RolledBack` status.
- [ ] Add rollback request/result DTO, including table deletion result fields.
- [ ] Add app service and repository rollback method signatures.

### Task 2: Backend Implementation

**Files:**
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] Implement safe file deletion for recorded generated paths.
- [ ] Implement generated menu and role-menu cleanup.
- [ ] Implement optional `dropTable` cleanup with safe table-name validation.
- [ ] Update history status to `RolledBack`.
- [ ] Add rollback endpoint and permission seed.

### Task 3: Frontend Governance Action

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [ ] Add rollback API call.
- [ ] Add rollback button in history list and detail drawer.
- [ ] Add rollback modal with explicit “delete business table and data” checkbox.
- [ ] Refresh list/detail after rollback.

### Task 4: Verification

**Files:**
- Create: `docs/features/2026-05-30-code-generator-artifact-governance/03-summary.md`

- [ ] Run code generator tests.
- [ ] Run frontend page check.
- [ ] Run frontend build.
- [ ] Restart backend and verify health.
