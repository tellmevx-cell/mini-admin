# 菜单管理 CRUD 任务执行文档

> 回补整理。

## 任务清单

- [x] 定义菜单实体和菜单 DTO
- [x] 实现菜单树查询
- [x] 实现菜单列表查询
- [x] 实现菜单新增、编辑、删除
- [x] 前端菜单管理页面
- [x] 角色分配复用菜单树
- [x] 种子数据补齐系统管理菜单

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Menu.cs`
- `src/MiniAdmin.Application.Contracts/Menus/MenuManagementItemDto.cs`
- `src/MiniAdmin.Application.Contracts/Menus/MenuTreeNodeDto.cs`
- `src/MiniAdmin.Application.Contracts/Menus/SaveMenuRequest.cs`
- `src/MiniAdmin.Application/Menus/MenuAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfMenuRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/menu.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/menu/index.vue`

## 执行步骤

1. 增加菜单 DTO 和保存请求。
2. 实现菜单 Repository 的树形查询和 CRUD。
3. API 暴露 `/system/menu/tree` 和 `/system/menu/list`。
4. API 增加菜单新增、编辑、删除路由。
5. 种子数据创建系统管理下的菜单和按钮。
6. 前端菜单页面用树表展示。
7. 角色页面读取菜单树用于权限分配。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminMenuDocs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Menu|Role"
```

## 当前状态

已完成。

