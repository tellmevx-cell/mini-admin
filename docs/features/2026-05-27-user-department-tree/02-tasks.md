# 用户管理与部门树筛选任务执行文档

> 回补整理。

## 任务清单

- [x] 用户实体增加部门、岗位、角色关系
- [x] 用户列表查询支持 `departmentId` 和 `positionId`
- [x] 用户新增/编辑支持部门、岗位、角色
- [x] 前端用户页面调整为左树右表
- [x] 删除用户增加自删除保护
- [x] 删除、编辑接入数据权限判断

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/Users/UserListQuery.cs`
- `src/MiniAdmin.Application.Contracts/Users/CreateUserRequest.cs`
- `src/MiniAdmin.Application.Contracts/Users/UpdateUserRequest.cs`
- `src/MiniAdmin.Application/Users/UserAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/department.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/position.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/role.ts`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 后端用户 DTO 增加 `DepartmentId`、`DepartmentName`、`PositionId`、`PositionName`、`Roles`。
2. 用户查询 Repository 增加部门和岗位过滤。
3. 用户创建和编辑时写入部门、岗位、角色关系。
4. 前端用户页面左侧加载部门树。
5. 点击部门节点后重新请求用户列表。
6. 新增/编辑抽屉加载部门、岗位、角色选项。
7. 删除用户时判断目标用户是否为当前用户。
8. 数据权限不足时返回明确错误。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminUserTreeDocs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "SystemUserList|UserList|UserManagement"
```

## 当前状态

已完成。

