# Tenant Initialization Template Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first enterprise-grade tenant provisioning loop with an initialization template, default tenant foundation data, and visible initialization status.

**Architecture:** Extend the existing tenant creation flow instead of adding a separate wizard. The repository keeps the transaction boundary, while a focused initialization service creates departments, positions, employee role, and safe menu grants for the new tenant.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL-compatible schema initializer, xUnit integration tests, Vue 3, Ant Design Vue, Vben.

---

### Task 1: Backend Contract And Failing Test

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/CreateTenantRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/TenantDto.cs`

- [ ] Add a failing integration test named `Tenant_Create_Initializes_Standard_Template_Foundation_Data`.
- [ ] The test should create a unique tenant with `initializationTemplateCode = "standard"`.
- [ ] The test should assert the returned tenant has `initializationTemplateCode = "standard"` and `initializationStatus = "Success"`.
- [ ] The test should query EF data and assert tenant departments `HQ`、`RD`、`MKT` exist.
- [ ] The test should assert tenant positions `dept-lead`、`developer`、`sales-manager` exist.
- [ ] The test should assert role `employee` exists under the tenant and has query permissions.
- [ ] Run the filtered test and confirm it fails before implementation.

### Task 2: Tenant Initialization Model

**Files:**
- Modify: `src/MiniAdmin.Domain/Entities/Tenant.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/CreateTenantRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/TenantDto.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] Add tenant initialization properties to the domain entity.
- [ ] Add DTO fields and request field.
- [ ] Configure EF property lengths.
- [ ] Add MySQL schema compatibility column creation for existing databases.
- [ ] Keep default values backward compatible.

### Task 3: Initialization Service

**Files:**
- Create: `src/MiniAdmin.Infrastructure/Persistence/TenantInitializationTemplateService.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs`

- [ ] Create a service that exposes template options and standard template initialization.
- [ ] Create default tenant departments, positions, and employee role.
- [ ] Assign employee role safe query menu permissions.
- [ ] Set tenant initialization status and time.
- [ ] Make repeated initialization idempotent within a tenant.

### Task 4: API And Frontend

**Files:**
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/ITenantAppService.cs`
- Modify: `src/MiniAdmin.Application.Contracts/MultiTenancy/ITenantRepository.cs`
- Modify: `src/MiniAdmin.Application/Tenants/TenantAppService.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue`

- [ ] Add `GET /platform/tenant/initialization-templates`.
- [ ] Load template options on the tenant management page.
- [ ] Add a template select to the create tenant modal only.
- [ ] Display initialization status in the tenant list.
- [ ] Keep edit tenant behavior unchanged.

### Task 5: Verification And Docs

**Files:**
- Modify: `docs/features/2026-05-29-tenant-initialization-template/02-tasks.md`
- Modify: `docs/features/2026-05-29-tenant-initialization-template/03-summary.md`

- [ ] Run filtered tenant initialization tests.
- [ ] Run full backend tests.
- [ ] Run frontend build.
- [ ] Start backend and frontend.
- [ ] Update summary with implementation and verification results.
