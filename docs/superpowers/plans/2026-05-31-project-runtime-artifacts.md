# Project Runtime Artifacts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add build history tracking and artifact visibility to the project runtime management page.

**Architecture:** Persist build history in a local JSON file beside project runtime configuration. Extend service DTOs with latest build and artifact data, and render those details in the existing Vben page.

**Tech Stack:** .NET 10, ASP.NET Minimal APIs, Vue 3, Ant Design Vue, Vben Admin.

---

### Task 1: Backend Contracts

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/ProjectRuntimeDtos.cs`
- Modify: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/IProjectRuntimeAppService.cs`

- [ ] Add build history and artifact DTOs.
- [ ] Add `BuildArtifactPath` to service DTOs and save request.
- [ ] Add service methods for build history and artifact lookup.

### Task 2: Backend Persistence

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/ProjectRuntimes/ProjectRuntimeAppService.cs`

- [ ] Add local JSON history file at `data/project-runtime/build-history.json`.
- [ ] Write a running record when build starts.
- [ ] Update the record on process exit.
- [ ] Compute artifact existence, type, size, and modified time.

### Task 3: API Endpoints

**Files:**
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Add build history endpoint.
- [ ] Add artifact endpoint.

### Task 4: Frontend Rendering

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/system/project-runtime.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/project-runtime/index.vue`

- [ ] Add TypeScript types and API functions.
- [ ] Render latest build and artifact information.
- [ ] Refresh overview after build state changes.

### Task 5: Tests And Verification

**Files:**
- Modify: `tests/MiniAdmin.Tests/ProjectRuntimeAppServiceTests.cs`
- Create: `docs/features/2026-05-31-project-runtime-artifacts/03-summary.md`

- [ ] Add test for artifact info.
- [ ] Add test for build history endpoint service method.
- [ ] Run backend build and targeted tests.
- [ ] Run frontend build.
- [ ] Confirm frontend and backend services are running.
