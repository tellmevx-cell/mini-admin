# MiniAdmin Docs Site Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an independent VitePress documentation site for MiniAdmin that introduces the project, explains usage, and helps developers customize it safely.

**Architecture:** Add a root-level `docs-site` package so documentation can be developed and published without modifying the upstream Vben docs package. The site uses VitePress default theme with custom brand styling, focused navigation, and Markdown pages organized by user journey: introduction, getting started, features, developer guide, runbooks, and FAQ.

**Tech Stack:** VitePress, Vue, Markdown, Mermaid diagrams, PowerShell-friendly commands.

---

### Task 1: Create VitePress Site Skeleton

**Files:**
- Create: `docs-site/package.json`
- Create: `docs-site/index.md`
- Create: `docs-site/.vitepress/config.mts`
- Create: `docs-site/.vitepress/theme/index.ts`
- Create: `docs-site/.vitepress/theme/custom.css`

- [x] **Step 1: Create package scripts**

Add package scripts for local development, production build, and preview.

- [x] **Step 2: Configure VitePress**

Configure site title, description, navigation, sidebar groups, search, outline, edit link placeholder, and Mermaid support through Markdown.

- [x] **Step 3: Add custom theme**

Use a restrained enterprise admin visual system: deep blue ink, cyan accent, clean landing sections, readable docs pages, and responsive homepage composition.

### Task 2: Write Homepage and Getting Started Docs

**Files:**
- Create: `docs-site/guide/introduction.md`
- Create: `docs-site/guide/quick-start.md`
- Create: `docs-site/guide/local-development.md`
- Create: `docs-site/guide/deployment.md`

- [x] **Step 1: Write project positioning**

Explain MiniAdmin as a .NET + Vben enterprise admin starter focused on RBAC, SaaS tenant, workflow, message center, audit, monitoring, files, and code generation.

- [x] **Step 2: Write quick start**

Document prerequisites, backend startup, frontend startup, login, and common startup issues.

- [x] **Step 3: Write deployment path**

Document build commands, configuration checklist, database mode, notification channels, and release checks.

### Task 3: Write Feature Docs

**Files:**
- Create: `docs-site/features/overview.md`
- Create: `docs-site/features/auth-rbac.md`
- Create: `docs-site/features/tenant.md`
- Create: `docs-site/features/workflow.md`
- Create: `docs-site/features/message-center.md`
- Create: `docs-site/features/code-generator.md`
- Create: `docs-site/features/monitoring-audit.md`
- Create: `docs-site/features/file-storage.md`

- [x] **Step 1: Write feature overview**

List platform modules with purpose, entry points, and extension notes.

- [x] **Step 2: Write detailed feature pages**

Document how each platform feature is used and what developers should know before extending it.

### Task 4: Write Developer Guide

**Files:**
- Create: `docs-site/developer/architecture.md`
- Create: `docs-site/developer/backend.md`
- Create: `docs-site/developer/frontend.md`
- Create: `docs-site/developer/database.md`
- Create: `docs-site/developer/add-module.md`
- Create: `docs-site/developer/conventions.md`

- [x] **Step 1: Explain layered architecture**

Document `Domain`, `Application.Contracts`, `Application`, `Infrastructure`, `Api`, tests, and Vben frontend responsibilities.

- [x] **Step 2: Explain extension workflow**

Document how to add an entity, DTO, app service, endpoint, menu seed, frontend API, frontend page, tests, and docs.

- [x] **Step 3: Document conventions**

Document permission naming, response shape, menu/page conventions, tenancy, audit, tests, and docs rules.

### Task 5: Integrate Runbooks and Verify Build

**Files:**
- Create: `docs-site/runbooks/workflow-message-center.md`
- Create: `docs-site/runbooks/acceptance.md`
- Create: `docs-site/faq.md`

- [x] **Step 1: Add operational docs**

Summarize workflow/message center operation and acceptance guidance while linking to the authoritative runbooks in `docs/runbooks`.

- [x] **Step 2: Install docs dependencies**

Run `pnpm install` inside `docs-site`.

- [x] **Step 3: Build docs**

Run `pnpm build` inside `docs-site` and fix any VitePress errors.
