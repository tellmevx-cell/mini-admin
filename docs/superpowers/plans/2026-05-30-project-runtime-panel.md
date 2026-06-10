# Project Runtime Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a first-version multi-project runtime panel for registering local projects, starting services, stopping services, and reading logs.

**Architecture:** Keep persistent project definitions in a local JSON file and keep process runtime state in memory. Expose Minimal API endpoints under `/system/project-runtime`, seed a Development Tools menu item, and build a Vben page that follows the current system monitor style.

**Tech Stack:** .NET 10 Minimal API, C# Process APIs, JSON file storage, Vben Vue 3, Ant Design Vue.

---

### Task 1: Backend Contracts

**Files:**
- Create: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/IProjectRuntimeAppService.cs`
- Create: `src/MiniAdmin.Application.Contracts/ProjectRuntimes/ProjectRuntimeDtos.cs`

- [ ] Define DTOs for project, workspace, service, state, log, and save requests.
- [ ] Define app service methods for overview, project CRUD, workspace start/stop, service start/stop/restart, and logs.

### Task 2: Runtime Service

**Files:**
- Create: `src/MiniAdmin.Infrastructure/ProjectRuntimes/ProjectRuntimeAppService.cs`

- [ ] Load and save JSON config at `data/project-runtime/projects.json`.
- [ ] Seed MiniAdmin default project when config file is missing.
- [ ] Start services with `ProcessStartInfo`, redirect output to logs, and track PID in memory.
- [ ] Stop only processes tracked by the service.
- [ ] Detect port and health status.

### Task 3: API and Permissions

**Files:**
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] Register `IProjectRuntimeAppService`.
- [ ] Map `/system/project-runtime` endpoints.
- [ ] Seed `ProjectRuntime` menu under Development Tools and query/manage/log permissions.

### Task 4: Frontend

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/system/project-runtime.ts`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/views/system/project-runtime/index.vue`

- [ ] Add API types and methods.
- [ ] Build project list, workspace service cards, log panel, add project modal, and action buttons.
- [ ] Respect permissions with `hasAccessByCodes`.

### Task 5: Verification

**Commands:**
- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`
- `pnpm run build:antd`

