# Code Generator Runnable CRUD Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade the code generator so previewed/generated single-table CRUD modules include backend runtime registration, EF mapping, protected endpoints, menu seeds, and Vben CRUD pages.

**Architecture:** Add stable runtime extension points once, then make generated modules plug into those extension points with marker interfaces and reflection scanning. Keep generated files isolated under whitelisted folders and continue preview-first generation with default conflict blocking.

**Tech Stack:** ASP.NET Core Minimal API, EF Core, xUnit, Vben web-antd, Ant Design Vue.

---

### Task 1: Failing Runnable Preview Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

- [ ] **Step 1: Add failing tests**

Add tests:

```csharp
[Fact]
public async Task CodeGeneratorPreview_Includes_Runnable_Backend_Crud_Files()
```

Expected files:

```text
src/MiniAdmin.Infrastructure/Persistence/Generated/CustomerEntityTypeConfiguration.cs
src/MiniAdmin.Api/Generated/CustomerEndpoints.cs
src/MiniAdmin.Infrastructure/Persistence/Generated/CustomerMenuSeed.cs
```

Expected content:

```text
RequirePermission("business:customer:query")
RequirePermission("business:customer:create")
RequirePermission("business:customer:update")
RequirePermission("business:customer:delete")
entity.ToTable("mini_customer")
entity.HasKey(x => x.Id)
```

- [ ] **Step 2: Run red test**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorPreview_Includes_Runnable"
```

Expected: FAIL because renderer does not generate runtime endpoint/config/seed files yet.

---

### Task 2: Stable Runtime Extension Points

**Files:**
- Create: `src/MiniAdmin.Application.Contracts/CodeGenerators/GeneratedCrudMarkers.cs`
- Create: `src/MiniAdmin.Api/CodeGenerators/GeneratedCrudEndpointExtensions.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/GeneratedCrudSeedDefinition.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

- [ ] **Step 1: Add marker interfaces**

Create marker interfaces:

```csharp
public interface IGeneratedCrudAppService;
public interface IGeneratedCrudRepository;
```

- [ ] **Step 2: Add endpoint extension**

Create API interface:

```csharp
public interface IGeneratedCrudEndpointDefinition
{
    void MapEndpoints(IEndpointRouteBuilder endpoints);
}
```

Add `AddGeneratedCrudServices` scanning generated AppServices and Repositories by marker interface.

Add `MapGeneratedCrudEndpoints` scanning endpoint definition types in the API assembly.

- [ ] **Step 3: Wire stable entry points**

In `Program.cs`:

```csharp
builder.Services.AddGeneratedCrudServices();
app.MapGeneratedCrudEndpoints();
```

In `MiniAdminDbContext.OnModelCreating`:

```csharp
modelBuilder.ApplyConfigurationsFromAssembly(typeof(MiniAdminDbContext).Assembly);
```

In initializer:

```csharp
await SeedGeneratedCrudModulesAsync(cancellationToken);
```

---

### Task 3: Runnable Template Rendering

**Files:**
- Modify: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`

- [ ] **Step 1: Render EF configuration**

Generate:

```text
src/MiniAdmin.Infrastructure/Persistence/Generated/{ModuleName}EntityTypeConfiguration.cs
```

Content includes:

```csharp
entity.ToTable("{request.TableName}");
entity.HasKey(x => x.Id);
```

- [ ] **Step 2: Render endpoint definition**

Generate:

```text
src/MiniAdmin.Api/Generated/{ModuleName}Endpoints.cs
```

Content maps list/create/update/delete endpoints and uses:

```csharp
.RequirePermission("{permissionPrefix}:query")
```

- [ ] **Step 3: Render menu seed**

Generate:

```text
src/MiniAdmin.Infrastructure/Persistence/Generated/{ModuleName}MenuSeed.cs
```

Content inserts menu and button permissions with deterministic GUIDs.

- [ ] **Step 4: Render stronger frontend CRUD page**

Generated page includes query bar, table, modal form, create/update/delete APIs, and `useAccess` permission checks.

---

### Task 4: Verification

**Files:**
- Modify: `docs/features/2026-05-29-code-generator-runnable-crud/02-tasks.md`
- Create: `docs/features/2026-05-29-code-generator-runnable-crud/03-summary.md`

- [ ] **Step 1: Run filtered tests**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
```

Expected: PASS.

- [ ] **Step 2: Run full backend tests**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

Expected: PASS.

- [ ] **Step 3: Run frontend build**

Run:

```powershell
pnpm run build:antd
```

Expected: PASS.

---

## Self Review

- Spec coverage: covers runnable backend files, stable registration, EF config, endpoint protection, menu seed, and frontend CRUD.
- Placeholder scan: no open placeholders.
- Scope: single-table CRUD only; excludes migration generation, tree table, master-detail, and import/export.
