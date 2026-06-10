# 日志管理与系统监控菜单重整任务执行文档

## 任务清单

- [x] 梳理当前菜单 seed 和动态菜单返回结构
- [x] 编写目标菜单结构失败测试
- [x] 新增菜单 ID 常量
- [x] 新增 seed version 执行菜单结构迁移
- [x] 补齐已授权角色的新父菜单授权
- [x] 运行菜单结构测试
- [x] 运行后端完整测试
- [x] 启动后端并验证健康检查
- [x] 补总结文档

## 涉及文件

### 后端

- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-05-28-menu-monitor-log-restructure/01-requirements.md`
- `docs/features/2026-05-28-menu-monitor-log-restructure/02-tasks.md`
- `docs/features/2026-05-28-menu-monitor-log-restructure/03-summary.md`

## 实现策略

- 保留旧的 `LogManagementMenuId` 作为“操作日志”页面，避免已有角色失去操作日志页面授权。
- 新增 `LogCenterMenuId` 作为一级“日志管理”父菜单。
- 新增 `SystemMonitorMenuId` 作为一级“系统监控”父菜单。
- 登录日志移动到日志管理下。
- 在线用户、权限诊断移动到系统监控下。
- 如果角色已经拥有某个子菜单授权，自动补齐对应新父菜单授权。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMenuRestructureTests'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "MenuAll_Returns_Common_System_Management_Menus_With_Token|MenuAll_Returns_Log_And_Monitor_Menu_Groups_With_Token"
```

完整测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMenuRestructureFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

## 验证记录

### 红灯测试

新增菜单结构测试后，旧实现失败：

- 一级菜单中没有 `LogManagement` 和 `SystemMonitor`。
- `System` 下仍包含 `LogManagement`、`LoginLog`、`OnlineUser`、`PermissionDiagnostics`。

### 菜单结构测试

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMenuRestructureGreen'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "MenuAll_Returns_Common_System_Management_Menus_With_Token|MenuAll_Returns_Log_And_Monitor_Menu_Groups_With_Token"
```

结果：2 个测试通过。

### 后端完整测试

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMenuRestructureFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：70 个测试通过。

### 启动健康检查

```powershell
Invoke-RestMethod -Uri http://localhost:5320/health
```

结果：`MiniAdmin.Api Healthy`。
