# Feature Showcase Screenshots Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a repeatable local automation that captures MiniAdmin feature screenshots, publishes them into the VitePress docs site, refreshes feature documentation, commits the changes, and pushes them to GitHub.

**Architecture:** Add a Node script under `scripts/` that uses the existing frontend Playwright dependency. The script logs into the local MiniAdmin web app, visits a curated showcase route list, captures screenshots into `docs-site/features/screenshots`, and writes machine-readable metadata for docs reuse.

**Tech Stack:** Node.js ESM, Playwright, VitePress Markdown, existing MiniAdmin local API and Vben frontend.

---

### Task 1: Add Screenshot Automation

**Files:**
- Create: `scripts/capture-feature-screenshots.mjs`
- Create: `docs-site/features/screenshots/.gitkeep`
- Create: `docs-site/features/screenshots/screenshots.json`

- [ ] Create a Playwright script that reads `MINIADMIN_WEB_URL`, `MINIADMIN_USERNAME`, `MINIADMIN_PASSWORD`, and optional `MINIADMIN_TENANT_CODE`.
- [ ] Define a curated route list covering login, dashboard, users, roles, tenants, workflow center, messages, file storage, audit logs, monitor, code generator, project runtime, and sample business pages.
- [ ] Login through the UI with default `admin / 123456`.
- [ ] Visit each route, wait for page stability, capture a screenshot, and append success or failure metadata.
- [ ] Save screenshots under `docs-site/features/screenshots`.

### Task 2: Add Feature Showcase Documentation

**Files:**
- Create: `docs-site/features/showcase.md`
- Modify: `docs-site/.vitepress/config.mts`
- Modify: `docs-site/features/overview.md`
- Modify: `README.md`

- [ ] Add a new VitePress feature page that groups screenshots by platform area.
- [ ] Add the showcase page to the features sidebar.
- [ ] Refresh the feature overview to include event bus, unit of work, API rate limiting, Docker/1Panel deployment, and showcase links.
- [ ] Add a README section pointing GitHub visitors to the feature screenshots and docs site.

### Task 3: Run, Verify, Commit, Push

**Files:**
- Verify generated screenshots in `docs-site/features/screenshots`.
- Verify docs build output.

- [ ] Start or reuse local API and frontend.
- [ ] Run `node scripts/capture-feature-screenshots.mjs`.
- [ ] Run `pnpm docs:build`.
- [ ] Run backend targeted validation for recent rate-limit changes.
- [ ] Stage only intended source, docs, and generated screenshot files.
- [ ] Commit the feature showcase and API rate-limit work.
- [ ] Push `main` to `origin`.
