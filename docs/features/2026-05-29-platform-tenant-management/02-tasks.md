# 平台租户管理任务执行文档

## 任务清单

- [x] 后端测试：新增租户列表、新增、编辑、禁用集成测试。
- [x] 契约：新增租户 DTO、查询、创建和更新请求。
- [x] 应用服务：新增 `TenantAppService`。
- [x] 仓储：扩展 `ITenantRepository` 并实现租户 CRUD。
- [x] API：新增 `/platform/tenant/*` 接口。
- [x] 菜单：新增平台管理和租户管理菜单权限。
- [x] 前端 API：新增租户管理接口。
- [x] 前端页面：新增租户列表和编辑弹窗。
- [x] 文档：完成功能总结。
- [x] 验证：运行后端测试、前端构建并启动服务。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 新增平台租户管理测试 |
| `src/MiniAdmin.Application.Contracts/Tenants/*` | 新增 | 租户管理契约 |
| `src/MiniAdmin.Application/Tenants/TenantAppService.cs` | 新增 | 租户应用服务 |
| `src/MiniAdmin.Application.Contracts/MultiTenancy/ITenantRepository.cs` | 修改 | 增加 CRUD 方法 |
| `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs` | 修改 | 实现 CRUD |
| `src/MiniAdmin.Api/Program.cs` | 修改 | 新增平台租户接口 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | 新增菜单权限种子 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs` | 修改 | 新增菜单权限 ID |
| `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts` | 新增 | 前端 API |
| `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue` | 新增 | 前端页面 |

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "PlatformTenant"
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

- 状态：功能已实现，完整验证通过，后端和前端已启动。

## 执行记录

- 新增 `PlatformTenant_List_Returns_Default_Demo_Tenant`，验证平台管理员能查询默认 `demo` 租户。
- 新增 `PlatformTenant_Can_Create_Update_And_Disable_Tenant`，验证新增、编辑和禁用租户。
- 禁用租户时会刷新该租户用户的 `SecurityStamp`，清理授权缓存，并把在线会话标记为离线。
- 前端新增 `平台管理 / 租户管理` 对应页面，按钮通过 `platform:tenant:*` 权限码控制。
- 后端筛选测试：`dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "PlatformTenant"`，2 个测试通过。
- 后端完整测试：`dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`，106 个测试通过。
- 前端构建：`pnpm run build:antd` 通过。
- 后端健康检查：`http://localhost:5320/health` 返回 `Healthy`。
- 前端服务：`http://localhost:5666/` 返回 200。
