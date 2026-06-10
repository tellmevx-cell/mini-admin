# 权限诊断中心任务执行文档

## 任务清单

- [x] 编写权限诊断接口失败测试
- [x] 新增后端 DTO 和接口契约
- [x] 新增应用服务
- [x] 新增 EF 仓储实现
- [x] 新增 API 路由
- [x] 新增菜单和权限种子
- [x] 新增前端 API
- [x] 新增前端页面
- [x] 启动初始化时刷新种子用户授权缓存
- [x] 完整测试和前端构建验证

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/PermissionDiagnostics/PermissionDiagnosticsDto.cs`
- `src/MiniAdmin.Application.Contracts/PermissionDiagnostics/IPermissionDiagnosticsRepository.cs`
- `src/MiniAdmin.Application.Contracts/PermissionDiagnostics/IPermissionDiagnosticsAppService.cs`
- `src/MiniAdmin.Application/PermissionDiagnostics/PermissionDiagnosticsAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfPermissionDiagnosticsRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/permission-diagnostics.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/permission-diagnostics/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 新增 `PermissionDiagnostics_Returns_Effective_User_Authorization` 测试。
2. 新增 `PermissionDiagnostics_Can_Refresh_User_Authorization_Cache` 测试。
3. 确认两个测试因接口 404 失败。
4. 新增后端 DTO、Repository、AppService。
5. 新增最小 API 路由并加权限保护。
6. 在种子数据中新增菜单和两个权限按钮。
7. 新增前端 API 和诊断页面。
8. 增加启动初始化时清理 `admin/demo/auditor` 授权缓存。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminPermissionDiagnosticsGreen'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "PermissionDiagnostics"
```

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminPermissionDiagnosticsFinalFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成。
