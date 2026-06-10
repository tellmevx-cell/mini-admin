# 文件存储一致性检查任务执行文档

## 任务清单

- [x] 明确任务边界：只检查缺失，不自动修复。
- [x] 编写后端红灯测试。
- [x] 扩展文件存储抽象，增加存在性检查。
- [x] 实现本地存储和 MinIO 存在性检查。
- [x] 扩展定时任务执行器，支持 `storage-consistency-check`。
- [x] 新增 seed 版本和内置任务。
- [x] 调整前端 `Warning` 状态展示。
- [x] 运行后端测试和前端构建。
- [x] 补总结文档。

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/Files/IFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/Storage/IFileStorageProvider.cs`
- `src/MiniAdmin.Infrastructure/Storage/LocalFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/Storage/MinioFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/Storage/CompositeFileStorageService.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/views/system/scheduled-job/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminStorageConsistencyJob'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "StorageConsistency"
```

结果：

```text
已通过! - 失败: 0，通过: 2，已跳过: 0，总计: 2
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminStorageConsistencyJobFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：

```text
已通过! - 失败: 0，通过: 75，已跳过: 0，总计: 75
```

前端构建：

```powershell
pnpm run build:antd
```

结果：

```text
Tasks: 11 successful, 11 total
```
