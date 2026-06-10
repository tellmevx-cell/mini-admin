# Project Runtime Build Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add service-level package/build actions and improve the log console so runtime/build output stays inside a fixed viewport and can follow latest lines.

**Architecture:** Extend the existing ProjectRuntime application service with build configuration, build state, build process execution, and build log reading. Reuse the current Vben Project Runtime page and keep the interaction service-centric.

**Tech Stack:** .NET 10, ASP.NET Minimal APIs, Vue 3, Ant Design Vue, Vben Admin.

---

### Task 1: Backend Contracts

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/ProjectRuntimeDtos.cs`
- Modify: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/IProjectRuntimeAppService.cs`

- [ ] Add build configuration fields and `ProjectRuntimeBuildStateDto`.
- [ ] Add `BuildServiceAsync` and `GetServiceBuildLogsAsync`.

### Task 2: Backend Implementation

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/ProjectRuntimes/ProjectRuntimeAppService.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Store build processes in a separate dictionary.
- [ ] Execute build command with redirected output.
- [ ] Add default build commands for MiniAdmin API and Vben Web.
- [ ] Add `/build` and `/build-logs` endpoints.

### Task 3: Frontend API And Page

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/project-runtime.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/project-runtime/index.vue`

- [ ] Add build fields to TypeScript interfaces.
- [ ] Add `buildProjectRuntimeServiceApi` and `getProjectRuntimeServiceBuildLogsApi`.
- [ ] Add service build button.
- [ ] Add log mode switch.
- [ ] Fix log console height and auto-scroll behavior.

### Task 4: Tests And Verification

**Files:**
- Modify: `tests/MiniAdmin.Tests/ProjectRuntimeAppServiceTests.cs`
- Create: `docs/features/2026-05-31-project-runtime-build/03-summary.md`

- [ ] Test build log reading.
- [ ] Run `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`.
- [ ] Run `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter ProjectRuntimeAppServiceTests`.
- [ ] Run `pnpm run build:antd`.
- [ ] Confirm backend health and frontend availability.
