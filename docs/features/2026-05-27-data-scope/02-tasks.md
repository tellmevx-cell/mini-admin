# 数据权限任务执行文档

> 回补整理。

## 任务清单

- [x] 角色模型增加数据范围字段
- [x] 新增 `IDataScopeProvider`
- [x] 用户列表接入数据权限过滤
- [x] 用户编辑接入目标数据权限校验
- [x] 用户删除接入目标数据权限校验
- [x] 权限诊断展示数据权限结果
- [x] 增加数据权限测试覆盖

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Role.cs`
- `src/MiniAdmin.Application.Contracts/DataScopes/IDataScopeProvider.cs`
- `src/MiniAdmin.Application.Contracts/DataScopes/DataScopeContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfDataScopeProvider.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- `src/MiniAdmin.Application/Users/UserAppService.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/views/system/role/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 在角色创建和编辑请求中增加数据范围。
2. Repository 保存角色数据范围。
3. `EfDataScopeProvider` 根据当前用户名解析角色数据范围。
4. 用户列表查询追加数据范围过滤。
5. 用户编辑和删除前判断目标用户是否可见。
6. 前端角色表单增加数据范围选择。
7. 测试本部门角色不能删除其他部门用户。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminDataScopeDocs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "DataScope"
```

## 当前状态

已完成。

