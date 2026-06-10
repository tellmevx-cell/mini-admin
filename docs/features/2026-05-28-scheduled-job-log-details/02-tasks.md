# 定时任务执行详情任务执行文档

## 任务清单

- [x] 明确详情只记录异常对象，不记录全部成功对象。
- [x] 编写后端红灯测试。
- [x] 新增执行详情领域实体和 EF 映射。
- [x] 扩展定时任务契约，支持执行结果携带详情。
- [x] 扩展仓储，执行日志和详情同事务写入。
- [x] 扩展文件存储一致性检查任务，写入异常详情。
- [x] 新增详情查询 API。
- [x] 新增数据库迁移。
- [x] 调整前端日志抽屉，支持查看详情。
- [x] 运行后端测试和前端构建。
- [x] 补总结文档。

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/ScheduledJobLogDetail.cs`
- `src/MiniAdmin.Domain/Entities/ScheduledJobLog.cs`
- `src/MiniAdmin.Application.Contracts/ScheduledJobs/ScheduledJobDtos.cs`
- `src/MiniAdmin.Application/ScheduledJobs/ScheduledJobAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfScheduledJobRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/scheduled-job.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/scheduled-job/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminScheduledJobLogDetails'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "ScheduledJobLogDetails"
```

结果：

```text
红灯：404 Not Found
绿灯：已通过! - 失败: 0，通过: 1，已跳过: 0，总计: 1
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminScheduledJobLogDetailsFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：

```text
已通过! - 失败: 0，通过: 76，已跳过: 0，总计: 76
```

前端构建：

```powershell
pnpm run build:antd
```

结果：

```text
Tasks: 11 successful, 11 total
```
