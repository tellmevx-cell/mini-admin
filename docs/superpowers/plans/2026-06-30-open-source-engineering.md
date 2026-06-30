# Open Source Engineering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the open-source engineering assets that make MiniAdmin easier to validate, run with Docker, deploy, and contribute to.

**Architecture:** Keep business code unchanged. Add CI, Docker packaging, environment examples, GitHub community templates, and documentation that wrap the existing .NET API, Vben frontend, and VitePress docs site.

**Tech Stack:** GitHub Actions, Docker, Docker Compose, .NET 10, Node.js 24, pnpm 11, MySQL 8, Redis 7, Nginx.

---

### Task 1: GitHub CI And Community Files

**Files:**
- Create: `.github/workflows/ci.yml`
- Create: `.github/ISSUE_TEMPLATE/bug_report.yml`
- Create: `.github/ISSUE_TEMPLATE/feature_request.yml`
- Create: `.github/pull_request_template.md`
- Create: `CONTRIBUTING.md`
- Create: `SECURITY.md`

- [ ] **Step 1: Add a CI workflow**

Create `.github/workflows/ci.yml` with jobs for backend restore/build/test, docs build, and frontend typecheck/build. Use `continue-on-error: true` for frontend checks if the Vben workspace dependency installation is too heavy for first-time contributors, but keep backend and docs strict.

- [ ] **Step 2: Add issue and PR templates**

Add structured bug report, feature request, and PR templates that ask for reproduction steps, affected modules, verification commands, and migration/menu/permission impacts.

- [ ] **Step 3: Add contribution and security policy**

Document branch naming, local setup, test commands, secret handling, default password rotation, and vulnerability disclosure expectations.

### Task 2: Docker And Compose

**Files:**
- Create: `Dockerfile.api`
- Create: `frontend/vue-vben-admin/apps/web-antd/Dockerfile`
- Create: `frontend/vue-vben-admin/apps/web-antd/nginx.conf`
- Create: `docker-compose.yml`
- Create: `.env.example`
- Modify: `.gitignore`

- [ ] **Step 1: Add API Dockerfile**

Build and publish `src/MiniAdmin.Api/MiniAdmin.Api.csproj` with the .NET 10 SDK image, then run it with the ASP.NET 10 runtime image on port 8080.

- [ ] **Step 2: Add web Dockerfile and Nginx config**

Build `@vben/web-antd` with pnpm 11 and serve the generated `dist` directory through Nginx. Proxy `/api/` to `mini-admin-api:8080`.

- [ ] **Step 3: Add Compose orchestration**

Compose should start MySQL, Redis, API, and Web. Use environment variable placeholders from `.env.example`, persistent volumes for MySQL/Redis/uploads, health checks, and safe defaults.

### Task 3: Documentation Updates

**Files:**
- Modify: `README.md`
- Modify: `docs-site/guide/deployment.md`
- Modify: `docs-site/guide/quick-start.md`
- Modify: `docs-site/.vitepress/config.mts`
- Create: `docs-site/guide/docker-compose.md`

- [ ] **Step 1: Add Docker quick start**

Document `copy .env.example .env`, password/JWT replacement, `docker compose up -d --build`, URLs, default accounts, logs, and teardown.

- [ ] **Step 2: Link new guide from docs site**

Add the Docker guide to the VitePress sidebar and cross-link it from quick start and deployment docs.

### Task 4: Verification

**Files:**
- No production file changes.

- [ ] **Step 1: Validate repository state**

Run `git status --short` and ensure only intended open-source engineering files changed.

- [ ] **Step 2: Validate backend**

Run `dotnet build MiniAdmin.slnx --no-restore` and `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --no-build`.

- [ ] **Step 3: Validate docs**

Run `pnpm --dir docs-site build`.

- [ ] **Step 4: Validate Docker config syntax**

Run `docker compose config` if Docker Compose is available.
