# Code Generator Field Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a two-step code-generator field selection flow: choose table columns first, then configure only selected fields.

**Architecture:** Reuse the existing `/system/code-generator/tables/{tableName}` backend API. The frontend keeps database column selections separate from generated field configs, and synchronizes them whenever the user checks or unchecks columns.

**Tech Stack:** Vue 3, Vben Admin, Ant Design Vue, existing MiniAdmin code generator API.

---

### Task 1: Add Selection State

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [x] Add a table field selection model based on `CodeGeneratorColumn`.
- [x] Add system-field detection for `id`, `tenant_id`, audit columns, and logical-delete columns.
- [x] Convert table columns to default field configs only when selected.

### Task 2: Add Two-Stage UI

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [x] Add a "表字段选择" table above "字段配置".
- [x] Add checkbox toggles for each column.
- [x] Add select all / unselect all buttons for generatable fields.
- [x] Keep custom fields available through "新增自定义字段".

### Task 3: Verify

**Files:**
- Modify: `docs/features/2026-05-29-code-generator-field-selection/02-tasks.md`
- Create: `docs/features/2026-05-29-code-generator-field-selection/03-summary.md`

- [x] Run `pnpm run build:antd`.
- [x] Update task and summary documents.
