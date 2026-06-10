# Code Generator Field Advanced Config Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make generated CRUD modules use field-level query, validation, dictionary, default value, and database constraint configuration.

**Architecture:** Extend the existing field config contract and keep generation centralized in `CodeGeneratorTemplateRenderer`. The preview page remains the source of configuration; backend templates translate those options into generated contracts, repositories, EF mappings, and Vben page controls.

**Tech Stack:** ASP.NET Core, EF Core, xUnit, Vue 3, Ant Design Vue, Vben.

---

### Task 1: Red Test

- [ ] Add a code generator preview test with advanced field metadata.
- [ ] Assert generated contracts include field-specific query properties.
- [ ] Assert generated repository filters use contains, equals, and range modes.
- [ ] Assert generated EF config includes max length and unique index.
- [ ] Assert generated Vben page includes advanced controls.

### Task 2: Backend Templates

- [ ] Extend `CodeGeneratorFieldConfigDto`.
- [ ] Generate query DTO properties from field config.
- [ ] Generate repository filters from field config.
- [ ] Generate EF max length and unique indexes.
- [ ] Preserve backward compatibility through default field config values.

### Task 3: Frontend Generator Page

- [ ] Extend TypeScript field config interface.
- [ ] Add default values when fields are created from table columns.
- [ ] Add UI columns for query mode, max length, unique, dictionary code, and default value.
- [ ] Keep preview clearing behavior.

### Task 4: Verification

- [ ] Run targeted advanced config test.
- [ ] Run all code generator tests.
- [ ] Run `impeccable` for the code generator page.
- [ ] Run `pnpm run build:antd`.
