# 租户资源配额闭环任务执行文档

## 任务清单

- [x] 明确配额语义、统计口径和兼容边界。
- [x] 新增配额快照、异常和服务契约。
- [x] 实现实时用量查询、租户锁和事务内配额校验。
- [x] 用户新增、导入预检和确认导入接入用户配额。
- [x] 文件实体、仓储和数据库迁移接入租户归属。
- [x] 文件上传和删除接入存储配额及失败补偿。
- [x] 租户列表和套餐页面展示用量与不限额语义。
- [x] 补齐 API 错误响应和租户文件隔离测试。
- [x] 执行后端全量测试、前端构建并编写总结文档。

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/TenantResourceQuotas/*`
- `src/MiniAdmin.Application/Users/UserAppService.cs`
- `src/MiniAdmin.Application/Files/FileAppService.cs`
- `src/MiniAdmin.Domain/Entities/ManagedFile.cs`
- `src/MiniAdmin.Infrastructure/Persistence/TenantResourceQuotaService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfFileRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfTenantRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/Migrations/*`
- `src/MiniAdmin.Api/Endpoints/UserManagementEndpointExtensions.cs`
- `src/MiniAdmin.Api/Endpoints/FileAndAuditEndpointExtensions.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/tenant-package/index.vue`

### 测试

- `tests/MiniAdmin.Tests/TenantResourceQuotaTests.cs`
- 现有用户、文件、租户与迁移相关测试。

## 执行步骤

1. 定义配额快照和 `TenantResourceQuotaExceededException`。
2. 实现按租户锁定、实时统计和事务内执行方法。
3. 接入用户单个新增、导入预检与确认导入。
4. 增加文件 `TenantId`、租户过滤和数据库升级逻辑。
5. 接入上传双重校验、失败存储补偿与删除释放。
6. 扩展租户 DTO 和前端资源用量单元格。
7. 增加专项测试并运行完整验证。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter TenantResourceQuota
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm --dir frontend/vue-vben-admin run build:antd
```

## 当前状态

已完成。
