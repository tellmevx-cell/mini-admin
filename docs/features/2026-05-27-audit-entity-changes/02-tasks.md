# 实体变更审计任务执行文档

## 任务清单

- [x] 编写实体变更审计失败测试
- [x] 新增实体变更领域模型
- [x] 新增 DTO 与采集接口
- [x] 在 DbContext 中捕获实体变化
- [x] 在审计日志仓储中保存实体变更
- [x] MySQL 初始化新表
- [x] 前端详情页展示数据变更
- [x] 测试和构建验证

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/AuditEntityChange.cs`
- `src/MiniAdmin.Domain/Entities/AuditLog.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/AuditEntityChangeDto.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/CapturedAuditEntityChange.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/IAuditEntityChangeCollector.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/AuditLogDto.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/SaveAuditLogRequest.cs`
- `src/MiniAdmin.Infrastructure/Persistence/AuditEntityChangeCollector.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAuditLogRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Api/AuditLogMiddleware.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/audit-log.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/log/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 新增 `SystemAuditLog_Records_Entity_Change_Diff_For_User_Update` 测试。
2. 确认测试因缺少 `EntityChanges` 失败。
3. 新增 `AuditEntityChange` 表模型。
4. 新增采集器 `IAuditEntityChangeCollector`。
5. 在 `AuditLogMiddleware` 中开启和关闭采集。
6. 在 `MiniAdminDbContext.SaveChangesAsync` 中捕获变化。
7. 在 `EfAuditLogRepository.CreateAsync` 中关联保存实体变更。
8. 在审计详情页展示修改前、修改后、字段差异。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminEntityAuditFocused'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "SystemAuditLog|AuditLog_Export"
```

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminEntityAuditFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成。
