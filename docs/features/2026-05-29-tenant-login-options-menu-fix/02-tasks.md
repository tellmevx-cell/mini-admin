# 租户登录选项和默认菜单修复任务执行文档

## 任务清单

- [x] 排查登录页租户下拉是否硬编码。
- [x] 排查租户管理员默认角色菜单来源。
- [x] 后端新增公开租户登录选项接口。
- [x] 前端登录页改为动态加载活跃租户。
- [x] 新增租户时补齐 `tenant-admin` 默认菜单。
- [x] 启动初始化时清理租户管理员权限缓存。
- [x] 增加回归测试并完成构建验证。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Api/Program.cs` | 修改 | 新增 `/auth/tenant-options` |
| `src/MiniAdmin.Application.Contracts/Tenants/TenantLoginOptionDto.cs` | 新增 | 登录页租户选项 DTO |
| `src/MiniAdmin.Application.Contracts/MultiTenancy/ITenantRepository.cs` | 修改 | 增加租户登录选项查询 |
| `src/MiniAdmin.Application.Contracts/Tenants/ITenantAppService.cs` | 修改 | 增加租户登录选项查询 |
| `src/MiniAdmin.Application/Tenants/TenantAppService.cs` | 修改 | 透传租户登录选项查询 |
| `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs` | 修改 | 查询活跃租户；新增租户时补齐管理员菜单 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | 清理租户管理员权限缓存 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts` | 修改 | 增加租户选项 API |
| `frontend/vue-vben-admin/apps/web-antd/src/views/_core/authentication/login.vue` | 修改 | 登录身份下拉动态加载租户 |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 增加租户登录选项和租户管理员菜单回归测试 |

## 验证记录

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantAdmin|TenantLoginOptions|PlatformTenant"`：5 个测试通过。
- `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`：112 个测试通过。
- `pnpm run build:antd`：前端构建通过。
- `http://localhost:5320/health`：后端返回 `Healthy`。
- `http://localhost:5666/`：前端返回 200。
- `http://localhost:5320/auth/tenant-options`：返回 `demo` 和 `jxnc`。
