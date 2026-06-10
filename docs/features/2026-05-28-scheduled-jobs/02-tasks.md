# 定时任务中心任务执行文档

## 任务清单

- [x] 梳理现有审计日志清理和菜单 seed
- [x] 编写后端红灯测试
- [x] 新增定时任务领域实体和迁移
- [x] 新增应用契约、应用服务、仓储和执行器
- [x] 新增后台调度服务
- [x] 新增 Minimal API 路由和权限保护
- [x] 新增菜单和内置任务 seed version
- [x] 新增 Vben API 和定时任务页面
- [x] 运行后端测试和前端构建
- [x] 启动后端并健康检查
- [x] 补总结文档

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/ScheduledJob.cs`
- `src/MiniAdmin.Domain/Entities/ScheduledJobLog.cs`
- `src/MiniAdmin.Application.Contracts/ScheduledJobs/*`
- `src/MiniAdmin.Application/ScheduledJobs/ScheduledJobAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfScheduledJobRepository.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- `src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobWorker.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/scheduled-job.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/scheduled-job/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 第一版设计

调度方式先使用简单间隔：

- `IntervalSeconds`：执行间隔，最小 60 秒。
- `NextRunAt`：下次执行时间。
- `IsEnabled`：是否启用。

内置任务：

```text
audit-log-cleanup
```

执行逻辑：删除 `CreatedAt < DateTimeOffset.UtcNow.AddDays(-90)` 的审计日志。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminScheduledJobs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "ScheduledJob"
```

完整后端测试：

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminScheduledJobsFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：

```text
已通过! - 失败: 0，通过: 73，已跳过: 0，总计: 73
```

前端构建：

```powershell
pnpm run build:antd
```

结果：

```text
Tasks: 11 successful, 11 total
```

健康检查：

```powershell
Invoke-RestMethod -Uri http://localhost:5320/health
```

结果：

```text
application: MiniAdmin.Api
status: Healthy
```
