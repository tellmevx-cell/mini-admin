# 系统监控看板任务执行文档

## 任务清单

- [x] 编写后端红灯测试。
- [x] 新增监控 DTO 和应用服务接口。
- [x] 实现监控应用服务。
- [x] 新增 `GET /system/monitor/overview` API。
- [x] 新增菜单和权限 seed。
- [x] 新增前端 API。
- [x] 新增 Vben 系统监控页面。
- [x] 根据页面评审优化系统监控页布局，修复宽屏横向溢出风险。
- [x] 补齐系统物理内存指标，区分系统内存和进程运行时内存。
- [x] 运行后端测试和前端构建。
- [x] 补总结文档。

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/SystemMonitor/SystemMonitorDtos.cs`
- `src/MiniAdmin.Application.Contracts/SystemMonitor/ISystemMonitorAppService.cs`
- `src/MiniAdmin.Infrastructure/SystemMonitor/SystemMonitorAppService.cs`
- `src/MiniAdmin.Infrastructure/DependencyInjection.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/monitor.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/monitor/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminSystemMonitor'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "SystemMonitor"
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminSystemMonitorFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

前端构建：

```powershell
pnpm run build:antd
```

## 验证结果

- 红灯测试：`SystemMonitorOverview_Returns_Runtime_Dependencies_And_Recent_Status` 初次失败为 `404 Not Found`。
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "SystemMonitor"`：通过，2/2。
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj`：通过，79/79。
- `pnpm run build:antd`：通过，`@vben/web-antd` 构建成功。
- 系统监控接口实测：`memory.totalPhysicalMemoryBytes`、`memory.availablePhysicalMemoryBytes`、`memory.usedPhysicalMemoryBytes`、`memory.physicalMemoryUsedPercent` 均正常返回。
