# 数据库迁移与初始化数据版本化任务执行文档

## 任务清单

- [x] 梳理当前初始化入口和模型配置
- [x] 新增迁移基线和种子数据版本表迁移
- [x] 新增 `DataSeedVersion` 实体和 DbContext 映射
- [x] 增加数据库结构管理配置项
- [x] 将 MySQL 初始化切换为 migration-first
- [x] 支持旧 MySQL 库基线接管
- [x] 将基础数据初始化改为版本化执行
- [x] 补充初始化行为测试
- [x] 更新配置示例和功能总结
- [x] 运行后端完整测试并验证启动健康检查

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/DataSeedVersion.cs`
- `src/MiniAdmin.Infrastructure/Persistence/DatabaseOptions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/Migrations/*`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Api/appsettings.json`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 当前设计

推荐采用“迁移基线 + 后续迁移”的方式：

- `InitialCreate` 迁移描述当前已有业务表。
- `AddDataSeedVersions` 迁移新增 seed 版本表。
- 新 MySQL 库会从零执行所有迁移。
- 旧 MySQL 库如果已有 `mini_users` 等业务表但没有 `__EFMigrationsHistory`，启动时会先写入 `InitialCreate` 的历史记录，再执行后续迁移。
- InMemory 仍走 `EnsureCreatedAsync()`，不参与 migrations。

## 风险控制

- 不删除任何已有表和已有业务数据。
- 不把初始化权限每次启动都重新授回，避免覆盖角色页面里的人工调整。
- 线上库首次启用迁移前，仍建议手动备份一次数据库。

## 验证计划

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMigrationTests'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

启动后验证：

```powershell
Invoke-RestMethod -Uri http://localhost:5320/health
```

## 验证记录

### 迁移相关测试

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMigrationFeatureTests'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "DatabaseInitializer_Records_Baseline_Seed_Version_Once|DatabaseInitializer_Does_Not_Regrant_Removed_Admin_Menus|DatabaseInitializer_Removes_AuditLogs_Older_Than_90_Days"
```

结果：3 个测试通过。

### 后端完整测试

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMigrationFeatureFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：69 个测试通过。

### 启动健康检查

```powershell
Invoke-RestMethod -Uri http://localhost:5320/health
```

结果：`MiniAdmin.Api Healthy`。
