# 租户数据隔离第一阶段任务执行文档

## 任务清单

- [x] 梳理当前 `ICurrentTenant`、用户、角色、部门、岗位仓储实现。
- [x] 编写需求文档。
- [x] 编写租户数据隔离回归测试，并确认测试先失败。
- [x] 给角色、部门、岗位实体和 EF 映射增加 `TenantId`。
- [x] 补 MySQL 兼容升级脚本，老库启动时自动增加 `TenantId` 字段和索引。
- [x] 用户仓储接入租户过滤、创建赋值和跨租户校验。
- [x] 角色仓储接入租户过滤、创建赋值和跨租户校验。
- [x] 部门仓储接入租户过滤、创建赋值和父子部门同租户校验。
- [x] 岗位仓储接入租户过滤、创建赋值和跨租户删除保护。
- [x] 运行后端测试、前端构建。
- [x] 编写总结文档。
- [x] 重启后端并验证前后端服务。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Domain/Entities/Role.cs` | 修改 | 增加 `TenantId` |
| `src/MiniAdmin.Domain/Entities/Department.cs` | 修改 | 增加 `TenantId` |
| `src/MiniAdmin.Domain/Entities/Position.cs` | 修改 | 增加 `TenantId` |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs` | 修改 | 增加租户字段映射和索引 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | 老库自动补字段和索引 |
| `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs` | 修改 | 用户租户隔离和跨租户校验 |
| `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs` | 修改 | 角色租户隔离 |
| `src/MiniAdmin.Infrastructure/Persistence/EfDepartmentRepository.cs` | 修改 | 部门租户隔离 |
| `src/MiniAdmin.Infrastructure/Persistence/EfPositionRepository.cs` | 修改 | 岗位租户隔离 |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 增加租户隔离集成测试 |

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantDataIsolation"
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

- RED：`TenantDataIsolation_TenantAdmin_SeesOnlyOwnCoreData_AndCreatesTenantUser` 首次运行失败，租户管理员访问系统管理接口返回 403。
- GREEN：补齐租户管理员系统管理基础权限，并完成用户、角色、部门、岗位租户过滤。
- 租户隔离测试：`dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantDataIsolation"`，1 个测试通过。
- 后端完整测试：`dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`，113 个测试通过。
- 前端构建：`pnpm run build:antd`，构建通过。
- 总结文档：已生成 `docs/features/2026-05-29-tenant-data-isolation/03-summary.md`。
- 后端服务：`http://localhost:5320/health` 返回 `MiniAdmin.Api Healthy`。
- 前端服务：`http://localhost:5666/` 返回 `200 OK`。
