# RBAC 权限体系任务执行文档

> 回补整理。

## 任务清单

- [x] 建立用户、角色、菜单、角色菜单关联模型
- [x] 新增角色 CRUD 接口
- [x] 新增角色分配菜单接口
- [x] 新增菜单树查询接口
- [x] 新增权限码返回接口
- [x] 接入后端权限码校验
- [x] 前端接入角色管理页面
- [x] 前端接入按钮权限判断
- [x] 修复权限取消后仍默认勾选的问题
- [x] 增加权限变更后的 token/cache 失效逻辑

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/User.cs`
- `src/MiniAdmin.Domain/Entities/Role.cs`
- `src/MiniAdmin.Domain/Entities/Menu.cs`
- `src/MiniAdmin.Domain/Entities/UserRole.cs`
- `src/MiniAdmin.Domain/Entities/RoleMenu.cs`
- `src/MiniAdmin.Application/Roles/RoleAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfMenuRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/role.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/menu.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/role/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/menu/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 定义 `Role`、`Menu`、`UserRole`、`RoleMenu` 实体关系。
2. 在数据库初始化中创建系统菜单、按钮权限和 admin 角色授权。
3. 实现角色列表、新增、编辑、删除接口。
4. 实现角色菜单读取和保存接口。
5. 实现 `/access/codes` 当前用户权限码接口。
6. 给系统接口逐步补上 `RequirePermission`。
7. 前端角色管理页面支持权限树勾选。
8. 前端按钮使用权限码控制显隐。
9. 修改用户角色或角色菜单后清理授权缓存并失效旧 token。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminRbacDocs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成。

