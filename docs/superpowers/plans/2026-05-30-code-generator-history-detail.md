# Code Generator History Detail Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a readable generation history detail flow that shows generation request, files, install plan, SQL draft, and next steps.

**Architecture:** Keep history list lightweight and add a detail endpoint by id. The repository deserializes stored request/files JSON, while the application service rebuilds the install plan so the detail page matches preview guidance.

**Tech Stack:** ASP.NET Core Minimal API, EF Core, xUnit, Vue 3, Ant Design Vue, Vben.

---

### Task 1: Backend Detail API

- [x] Add detail DTOs.
- [x] Add repository and application service methods.
- [x] Add minimal API endpoint.
- [x] Add tests for detail and not found.

### Task 2: Frontend Detail Drawer

- [x] Extend API typings.
- [x] Add detail loading state.
- [x] Add a drawer/modal from history table.
- [x] Render install plan, files, and request JSON.

### Task 3: Verification

- [x] Run code generator tests.
- [x] Run frontend page quality check.
- [x] Run Vben build.
- [x] Start/check backend and frontend.
