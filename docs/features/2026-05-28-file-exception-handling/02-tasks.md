# 文件异常处理任务执行文档

## 任务清单

- [x] 明确状态模型：`Normal / Missing / Invalid`。
- [x] 编写后端红灯测试。
- [x] 给文件领域实体、DTO、仓储增加状态。
- [x] 文件上传默认写入 `Normal`。
- [x] 文件一致性检查发现缺失时标记 `Missing`。
- [x] 下载 `Missing/Invalid` 文件时返回 `409 Conflict`。
- [x] 新增标记无效应用服务和 API。
- [x] 新增标记无效权限和 seed。
- [x] 新增数据库迁移。
- [x] 前端文件列表展示状态。
- [x] 前端任务详情支持标记无效。
- [x] 运行后端测试和前端构建。
- [x] 补总结文档。

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/ManagedFile.cs`
- `src/MiniAdmin.Application.Contracts/Files/*`
- `src/MiniAdmin.Application/Files/FileAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfFileRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/file.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/file/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/scheduled-job/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminFileExceptionHandling'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "FileException"
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminFileExceptionHandlingFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

前端构建：

```powershell
pnpm run build:antd
```

## 验证结果

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "FileException"`：通过，2/2。
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj`：通过，78/78。
- `pnpm run build:antd`：通过，`@vben/web-antd` 构建成功并生成 `dist.zip`。
