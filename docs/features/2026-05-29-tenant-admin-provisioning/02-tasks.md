# 租户管理员初始化任务执行文档

## 任务清单

- [x] 后端测试：新增租户时创建租户管理员，并验证可登录。
- [x] 后端测试：管理员用户名重复时新增租户失败。
- [x] 契约：扩展 `CreateTenantRequest` 管理员字段。
- [x] 种子：补齐 `tenant-admin` 内置角色。
- [x] 仓储：新增租户时同步创建管理员用户和角色关系。
- [x] 前端 API：扩展新增租户参数类型。
- [x] 前端页面：新增租户弹窗增加管理员信息区域。
- [x] 文档：完成功能总结。
- [x] 验证：运行后端测试、前端构建并启动服务。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 增加租户管理员初始化集成测试 |
| `src/MiniAdmin.Application.Contracts/Tenants/CreateTenantRequest.cs` | 修改 | 增加管理员信息字段 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs` | 修改 | 增加 `tenant-admin` 角色 ID |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | 补齐租户管理员角色种子 |
| `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs` | 修改 | 创建租户时创建管理员用户 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts` | 修改 | 扩展新增租户参数 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue` | 修改 | 新增管理员信息表单 |

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantAdmin"
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

- 状态：功能已实现，后端测试、前端构建和服务启动验证已通过。

## 执行记录

- 新增 `TenantAdmin_CreateTenant_Creates_Admin_And_Allows_Login`，验证新增租户会创建租户管理员并允许按租户编码登录。
- 新增 `TenantAdmin_CreateTenant_Rejects_Duplicate_Admin_UserName`，验证管理员用户名全局重复时返回 400。
- 新增 `tenant-admin` 内置角色，并默认赋予工作台菜单，避免租户管理员登录后无入口。
- 新增租户表单增加管理员账号、姓名、邮箱、初始密码字段。
- 后端相关测试：`dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "PlatformTenant|TenantAdmin"`，4 个测试通过。
- 后端完整测试：`dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`，110 个测试通过。
- 前端构建：`pnpm run build:antd`，构建通过。
- 后端服务：`http://localhost:5320/health` 返回 `Healthy`。
- 前端服务：`http://localhost:5666/` 返回 200。
