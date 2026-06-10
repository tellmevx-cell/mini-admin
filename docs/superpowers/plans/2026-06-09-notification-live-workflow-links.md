# Notification Live Workflow Links Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep notification unread state synchronized across the layout and message center while preserving workflow deep-link navigation.

**Architecture:** Add a focused Pinia store for recent notifications and unread count. The top layout will consume the store for polling and dropdown actions, while the message center will use store mutation helpers after paged list actions to keep the badge fresh.

**Tech Stack:** Vue 3, Pinia, Vben Admin, ant-design-vue, existing MiniAdmin notification APIs.

---

### Task 1: Shared Notification Store

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/store/notification.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/store/index.ts`

- [x] Add a Pinia store that loads recent notifications through `getMyNotificationsApi(20)`.
- [x] Expose `notifications`, `unreadCount`, `loading`, `showDot`, and action helpers.
- [x] Export the store from `src/store/index.ts`.

### Task 2: Layout Integration

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/layouts/basic.vue`

- [x] Replace local notification refs with the shared store.
- [x] Keep 30-second polling while logged in.
- [x] Refresh on dropdown open, window focus, and document visibility return.
- [x] Route notification links through `createRouteLocationFromLink`.

### Task 3: Message Center Synchronization

**Files:**
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`

- [x] Use the shared store to update unread count after page loads and actions.
- [x] Keep table pagination/query state local to the page.
- [x] Improve source group labels and helper copy for workflow, alert, and business messages.
- [x] Mark workflow notification links as normal internal links so existing approval drawer deep-link code opens the target instance.

### Task 4: Verification

**Files:**
- Modify: `docs/superpowers/plans/2026-06-09-notification-live-workflow-links.md`

- [x] Run `pnpm run build:antd`.
- [x] Run existing targeted backend notification/workflow tests with `--no-build` if the API is running.
- [x] Restart frontend/backend if needed.
- [x] Verify `http://localhost:5021/health` and `http://localhost:5666/`.
