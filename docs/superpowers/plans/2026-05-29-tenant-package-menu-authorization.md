# Tenant Package Menu Authorization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add SaaS tenant package menu authorization so tenant permissions are capped by the package assigned to each tenant.

**Architecture:** Keep `TenantPackage.MenuIds` as the package permission source. Apply the package cap in menu authorization and role assignment paths, and prune stale tenant role permissions when package menus shrink.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core, MySQL/InMemory, official Vben frontend with Ant Design Vue.

---

### Task 1: Feature Docs And Red Tests

**Files:**
- Create: `docs/features/2026-05-29-tenant-package-menu-authorization/01-requirements.md`
- Create: `docs/features/2026-05-29-tenant-package-menu-authorization/02-tasks.md`
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] Write requirements for package menu caps and automatic cleanup.
- [ ] Add a failing test named `TenantPackageAuthorization_TenantUserPermissions_AreCappedByPackage`.
- [ ] Add a failing test named `TenantPackageAuthorization_ShrinkingPackageMenus_RemovesTenantRoleMenus`.
- [ ] Run `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantPackageAuthorization"` and confirm tests fail because package caps are not implemented.

### Task 2: Backend Contracts And Repository

**Files:**
- Create: `src/MiniAdmin.Application.Contracts/TenantPackages/ITenantPackageAppService.cs`
- Create: `src/MiniAdmin.Application.Contracts/TenantPackages/ITenantPackageRepository.cs`
- Create: `src/MiniAdmin.Application.Contracts/TenantPackages/TenantPackageDtos.cs`
- Create: `src/MiniAdmin.Application/TenantPackages/TenantPackageAppService.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfTenantPackageRepository.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] Add DTOs for list items, save request, menu update request, and options.
- [ ] Add repository methods for list, options, create, update, set status, get menu ids, update menu ids.
- [ ] Register app service and repository.
- [ ] Add platform-protected endpoints under `/platform/tenant-package`.

### Task 3: Package Cap Enforcement

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfMenuRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/CreateTenantRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/UpdateTenantRequest.cs`
- Modify: `src/MiniAdmin.Application.Contracts/Tenants/TenantDto.cs`

- [ ] Apply `角色菜单 ∩ 套餐菜单` when computing tenant user menus and permission codes.
- [ ] Limit tenant role permission tree and saved role menu IDs to package menus.
- [ ] Add package selection to tenant create and update flows.
- [ ] Invalidate tenant user authorization cache when package changes.

### Task 4: Frontend

**Files:**
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant-package.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/system/tenant-package/index.vue`
- Modify: `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue`

- [ ] Add tenant package API client.
- [ ] Replace the placeholder page with package list, form, status switch, and permission tree modal.
- [ ] Add package column and package selector to tenant management.

### Task 5: Verification And Summary

**Files:**
- Create: `docs/features/2026-05-29-tenant-package-menu-authorization/03-summary.md`
- Modify: `docs/features/2026-05-29-tenant-package-menu-authorization/02-tasks.md`

- [ ] Run tenant package authorization tests.
- [ ] Run full backend tests.
- [ ] Run frontend build.
- [ ] Restart backend and verify `/health`.
- [ ] Verify frontend returns 200.
- [ ] Write summary and manual acceptance steps.
