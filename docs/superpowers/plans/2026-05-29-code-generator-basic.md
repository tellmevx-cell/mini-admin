# Code Generator Basic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first enterprise-grade code generator module that can inspect MySQL tables, preview safe CRUD output, record generation history, and expose a Vben management page.

**Architecture:** Keep the generator as a controlled engineering tool under System Management, not a free-form low-code engine. Contracts define metadata/config/preview/history shapes, Application coordinates validation and rendering, Infrastructure reads MySQL metadata and persists history, API protects endpoints with RBAC, and Vben provides a preview-first workflow.

**Tech Stack:** ASP.NET Core Minimal API, EF Core MySQL/InMemory, xUnit, official Vben web-antd, Ant Design Vue.

---

## File Structure

- Create `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`: table metadata, field config, preview, and history DTO records.
- Create `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorAppService.cs`: use-case contract for table list, table detail, preview, generate, history.
- Create `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`: metadata/history persistence boundary.
- Create `src/MiniAdmin.Domain/Entities/CodeGenerationHistory.cs`: generation audit record.
- Create `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`: validate config, normalize names, call renderer/repository.
- Create `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`: deterministic file preview rendering.
- Create `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`: EF history and MySQL `information_schema` reader.
- Modify `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`: add `CodeGenerationHistories` mapping.
- Modify `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`: register repository.
- Modify `src/MiniAdmin.Api/Program.cs`: add DI and `/system/code-generator/*` endpoints.
- Modify `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`: add menu and permission IDs.
- Modify `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`: seed generator menu under `系统管理 / 开发工具 / 代码生成`.
- Create `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`: API client.
- Create `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`: generator page.
- Modify `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`: add focused integration tests for preview, conflict blocking, history.
- Create `docs/features/2026-05-29-code-generator-basic/03-summary.md`: completion summary.

---

### Task 1: Contract And Failing Preview Tests

**Files:**
- Modify: `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- Create: `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- Create: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorAppService.cs`
- Create: `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`

- [ ] **Step 1: Write failing API tests**

Add tests named:

```csharp
[Fact]
public async Task CodeGeneratorPreview_Returns_Files_Permissions_And_NoConflicts()

[Fact]
public async Task CodeGeneratorGenerate_Blocks_Conflicting_Files_By_Default()

[Fact]
public async Task CodeGeneratorGenerate_Records_History_When_Successful()
```

Use `AuthorizeAsync()` first, then call:

```csharp
var response = await _client.PostAsJsonAsync("/system/code-generator/preview", new
{
    tableName = "crm_customer",
    moduleName = "Customer",
    businessName = "客户",
    routePath = "/business/customer",
    parentMenuId = (string?)null,
    permissionPrefix = "business:customer",
    tenantMode = "Tenant",
    fields = new[]
    {
        new
        {
            columnName = "customer_name",
            propertyName = "CustomerName",
            displayName = "客户名称",
            dotNetType = "string",
            tsType = "string",
            isPrimaryKey = false,
            isRequired = true,
            listVisible = true,
            queryVisible = true,
            createVisible = true,
            updateVisible = true,
            controlType = "Input",
            dictionaryCode = (string?)null,
            sort = 1
        }
    }
});
```

Expected preview:

```csharp
Assert.Equal(HttpStatusCode.OK, response.StatusCode);
Assert.Contains(json.Data.PermissionCodes, x => x == "business:customer:query");
Assert.Contains(json.Data.Files, x => x.RelativePath == "src/MiniAdmin.Domain/Entities/Customer.cs");
Assert.Contains(json.Data.Files, x => x.RelativePath == "frontend/vue-vben-admin/apps/web-antd/src/views/business/customer/index.vue");
Assert.DoesNotContain(json.Data.Files, x => x.HasConflict);
```

- [ ] **Step 2: Run red test**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
```

Expected: FAIL because `/system/code-generator/preview` does not exist.

- [ ] **Step 3: Add DTO and interface shells**

Add records for:

```csharp
CodeGeneratorTableDto
CodeGeneratorColumnDto
CodeGeneratorFieldConfigDto
CodeGeneratorPreviewRequest
CodeGeneratorPreviewFileDto
CodeGeneratorPreviewResultDto
CodeGeneratorGenerateRequest
CodeGenerationHistoryDto
CodeGeneratorHistoryListQuery
```

Add interfaces:

```csharp
Task<IReadOnlyList<CodeGeneratorTableDto>> GetTablesAsync(CancellationToken cancellationToken = default);
Task<CodeGeneratorTableDto?> GetTableAsync(string tableName, CancellationToken cancellationToken = default);
Task<CodeGeneratorPreviewResultDto> PreviewAsync(CodeGeneratorPreviewRequest request, CancellationToken cancellationToken = default);
Task<CodeGenerationHistoryDto> GenerateAsync(CodeGeneratorGenerateRequest request, Guid? operatorUserId, string? operatorUserName, CancellationToken cancellationToken = default);
Task<PageResult<CodeGenerationHistoryDto>> GetHistoriesAsync(CodeGeneratorHistoryListQuery query, CancellationToken cancellationToken = default);
```

---

### Task 2: Renderer, Validation, And Safe Paths

**Files:**
- Create: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- Create: `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`

- [ ] **Step 1: Implement preview path whitelist**

Allowed relative roots:

```csharp
"src/MiniAdmin.Domain/Entities/"
"src/MiniAdmin.Application.Contracts/"
"src/MiniAdmin.Application/"
"src/MiniAdmin.Infrastructure/Persistence/"
"src/MiniAdmin.Api/"
"frontend/vue-vben-admin/apps/web-antd/src/api/"
"frontend/vue-vben-admin/apps/web-antd/src/views/"
```

Reject generated paths containing `..`, rooted paths, drive separators, or backslash traversal.

- [ ] **Step 2: Render minimal deterministic CRUD files**

Render these first-phase files:

```text
src/MiniAdmin.Domain/Entities/{ModuleName}.cs
src/MiniAdmin.Application.Contracts/{ModuleNamePlural}/{ModuleName}Dtos.cs
src/MiniAdmin.Application.Contracts/{ModuleNamePlural}/I{ModuleName}AppService.cs
src/MiniAdmin.Application.Contracts/{ModuleNamePlural}/I{ModuleName}Repository.cs
src/MiniAdmin.Application/{ModuleNamePlural}/{ModuleName}AppService.cs
src/MiniAdmin.Infrastructure/Persistence/Ef{ModuleName}Repository.cs
frontend/vue-vben-admin/apps/web-antd/src/api/business/{routeSegment}.ts
frontend/vue-vben-admin/apps/web-antd/src/views/business/{routeSegment}/index.vue
```

Compute permission codes:

```text
{permissionPrefix}:query
{permissionPrefix}:create
{permissionPrefix}:update
{permissionPrefix}:delete
```

- [ ] **Step 3: Run green preview test**

Run the same `CodeGeneratorPreview` filtered test and confirm PASS.

---

### Task 3: Repository, History, And Generation Safety

**Files:**
- Create: `src/MiniAdmin.Domain/Entities/CodeGenerationHistory.cs`
- Create: `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`

- [ ] **Step 1: Add generation history entity**

Columns:

```csharp
Guid Id
string TableName
string ModuleName
string BusinessName
string PermissionPrefix
string TenantMode
string RequestJson
string FilesJson
string Status
string? ErrorMessage
Guid? OperatorUserId
string? OperatorUserName
DateTimeOffset CreatedAt
```

- [ ] **Step 2: Implement conflict blocking**

`GenerateAsync` must call preview first. If any file exists and `Overwrite == false`, return HTTP 400 through API with message containing `文件已存在`.

- [ ] **Step 3: Implement safe file write**

Write only preview files whose normalized full path stays inside `C:\monica\code\mini-admin`. Create directories as needed. Persist history with `Success` or `Failed`.

- [ ] **Step 4: Run red/green history tests**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGeneratorGenerate"
```

Expected after implementation: PASS.

---

### Task 4: MySQL Metadata Reader And API Endpoints

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- Modify: `src/MiniAdmin.Api/Program.cs`

- [ ] **Step 1: Read MySQL metadata**

For MySQL provider, query `information_schema.tables` and `information_schema.columns` using the active `MiniAdminDbContext` connection. For InMemory/Testing, return an empty table list and allow preview from manual config.

- [ ] **Step 2: Add protected endpoints**

Endpoints:

```text
GET  /system/code-generator/tables           system:code-generator:query
GET  /system/code-generator/tables/{name}    system:code-generator:query
POST /system/code-generator/preview          system:code-generator:preview
POST /system/code-generator/generate         system:code-generator:generate
GET  /system/code-generator/history          system:code-generator:query
```

---

### Task 5: Menu Seed And Vben Page

**Files:**
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- Modify: `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- Create: `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

- [ ] **Step 1: Seed menu and permissions**

Add `开发工具` directory under `系统管理`, then add `代码生成` page. Give admin role all generator permissions by seed.

- [ ] **Step 2: Build Vben page**

Use a restrained current-system style:

```text
Top query/config area
Left table list
Center field configuration table
Right preview/history tabs
Footer generate button
```

Keep first phase practical: manual config and preview/generate controls must work even if table metadata is empty in Testing.

---

### Task 6: Docs, Verification, And Startup

**Files:**
- Create: `docs/features/2026-05-29-code-generator-basic/03-summary.md`

- [ ] **Step 1: Write summary doc**

Summarize:

```text
Implemented scope
API endpoints
Permission codes
Generated file list
Safety constraints
Known phase-2 work
Verification commands
```

- [ ] **Step 2: Verify backend and frontend**

Run:

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
pnpm run build:antd
```

- [ ] **Step 3: Start services**

Start backend on `http://localhost:5320` and frontend on `http://localhost:5666`.

---

## Self Review

- Spec coverage: covers MySQL metadata, field config, preview, safe generation, history, RBAC, Vben page, and docs.
- Placeholder scan: no TBD/TODO placeholders.
- Scope: phase 1 intentionally avoids automatic EF migration editing, full generated module compilation, and destructive overwrite by default.
- Type consistency: DTO/service/repository names use `CodeGenerator` consistently.
