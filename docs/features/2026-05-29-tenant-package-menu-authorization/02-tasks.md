# 租户套餐菜单授权任务执行文档

## 任务清单

- [x] 编写设计规格文档。
- [x] 编写需求文档。
- [x] 编写租户套餐授权回归测试，并确认测试先失败。
- [x] 实现后端套餐管理接口。
- [x] 实现套餐菜单授权和自动清理。
- [x] 将租户菜单和权限计算接入套餐上限。
- [x] 将租户角色权限分配接入套餐上限。
- [x] 实现前端租户套餐页面。
- [x] 租户管理支持选择套餐。
- [x] 运行后端测试和前端构建。
- [x] 重启后端并验证前后端服务。
- [x] 编写总结文档。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Application.Contracts/TenantPackages/*` | 新增 | 套餐接口、DTO、请求对象 |
| `src/MiniAdmin.Application/TenantPackages/TenantPackageAppService.cs` | 新增 | 套餐应用服务 |
| `src/MiniAdmin.Infrastructure/Persistence/EfTenantPackageRepository.cs` | 新增 | 套餐仓储和清理逻辑 |
| `src/MiniAdmin.Infrastructure/Persistence/EfMenuRepository.cs` | 修改 | 用户菜单和权限接入套餐上限 |
| `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs` | 修改 | 角色授权树和保存接入套餐上限 |
| `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs` | 修改 | 租户创建、编辑支持套餐 |
| `src/MiniAdmin.Api/Program.cs` | 修改 | 增加套餐接口 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant-package.ts` | 新增 | 前端套餐 API |
| `frontend/vue-vben-admin/apps/web-antd/src/views/system/tenant-package/index.vue` | 修改 | 套餐管理页面 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue` | 修改 | 租户选择套餐 |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 套餐授权测试 |

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantPackageAuthorization"
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

- 状态：功能已实现，前后端服务已启动并验证可访问。

## 执行记录

- RED：`dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantPackageAuthorization"`，2 个测试失败。
- 失败 1：`/platform/tenant-package/{id}/menus` 返回 404，说明套餐菜单授权接口尚未实现。
- 失败 2：租户用户仍看到套餐外 `RoleManagement` 菜单，说明用户最终权限尚未接入套餐上限。
- GREEN：`dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantPackageAuthorization"`，2 个测试通过。
- 后端完整测试：`dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`，115 个测试通过。
- 前端构建：`pnpm run build:antd`，构建通过。
- 后端服务：`http://localhost:5320/health` 返回 `MiniAdmin.Api Healthy`。
- 前端服务：`http://localhost:5666/` 返回 `200 OK`。
- 总结文档：已生成 `docs/features/2026-05-29-tenant-package-menu-authorization/03-summary.md`。
- 修复补充：发现 admin 后端菜单树缺少 `TenantPackage`，已补初始化授权和 admin 授权缓存清理。
- 修复验证：`MenuAll_Returns_TenantPackage_Menu_For_Admin` 和系统菜单列表回归测试通过；后端完整测试 116 个通过。
