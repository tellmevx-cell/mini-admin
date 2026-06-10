# 代码生成器二期任务执行文档

## 任务清单

- [x] 编写失败测试：预览必须包含可运行 CRUD 所需文件。
- [x] 编写失败测试：预览生成 endpoint 必须包含 `RequirePermission`。
- [x] 编写失败测试：预览生成 EF 配置必须包含 `ToTable` 和主键。
- [x] 编写失败测试：租户模式必须生成租户字段、过滤和 EF 索引。
- [x] 新增生成服务标记接口。
- [x] 新增生成 Endpoint 自动挂载入口。
- [x] 新增生成 AppService/Repository 自动 DI 注册入口。
- [x] 新增生成菜单权限 Seed 自动执行入口。
- [x] DbContext 接入 `ApplyConfigurationsFromAssembly`。
- [x] 更新模板渲染器，生成 EntityTypeConfiguration。
- [x] 更新模板渲染器，生成 Api EndpointDefinition。
- [x] 更新模板渲染器，生成菜单权限 SeedDefinition。
- [x] 更新模板渲染器，生成完整前端 CRUD 页面。
- [x] 更新路径预览和冲突检测。
- [x] 运行 CodeGenerator 过滤测试。
- [x] 运行后端全量测试。
- [x] 运行前端构建。
- [x] 编写 `03-summary.md`。

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/CodeGenerators/GeneratedCrudMarkers.cs`
- `src/MiniAdmin.Api/CodeGenerators/GeneratedCrudEndpointExtensions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/GeneratedCrudSeedDefinition.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-05-29-code-generator-runnable-crud/01-requirements.md`
- `docs/features/2026-05-29-code-generator-runnable-crud/02-tasks.md`
- `docs/features/2026-05-29-code-generator-runnable-crud/03-summary.md`
- `docs/superpowers/plans/2026-05-29-code-generator-runnable-crud.md`

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成代码生成器二期实现与验证。后续可以在此基础上进入“生成后模块安装/数据库表同步/生成历史差异查看”等增强能力。
