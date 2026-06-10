# 系统告警中心任务执行文档

## 任务清单

- [x] 编写后端红灯测试。
- [x] 新增告警实体和 EF 配置。
- [x] 新增告警 DTO、仓储接口、应用服务接口。
- [x] 实现告警仓储和应用服务。
- [x] 新增告警查询和确认 API。
- [x] 将告警扫描接入定时任务执行器。
- [x] 新增菜单、权限和内置定时任务 seed。
- [x] 新增 EF Core migration，创建 `mini_alerts` 表。
- [x] 新增前端 API。
- [x] 新增 Vben 告警中心页面。
- [x] 运行后端测试和前端构建。
- [x] 补总结文档。

## 计划涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Alert.cs`
- `src/MiniAdmin.Application.Contracts/Alerts/AlertDtos.cs`
- `src/MiniAdmin.Application/Alerts/AlertAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAlertRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/Migrations/20260528080107_AddAlerts.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/alert.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/alert/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminAlertCenter'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Alert"
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminAlertCenterFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

前端构建：

```powershell
pnpm run build:antd
```

## 验证结果

- 红灯测试：`AlertList_Returns_Page_With_Token` 初次失败为 `404 Not Found`；`AlertScanJob_Creates_Abnormal_File_Alert_And_Admin_Can_Acknowledge` 初次失败为找不到 `alert-scan` 定时任务。
- 告警中心过滤测试：通过，2/2。
- 后端完整测试：通过，81/81。
- Vben 前端构建：通过，`@vben/web-antd` 构建成功。
