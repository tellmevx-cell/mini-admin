# 定时任务执行详情总结文档

## 本次完成

- 新增执行详情实体 `ScheduledJobLogDetail`。
- 新增数据库表 `mini_scheduled_job_log_details`。
- 新增迁移 `20260528051149_AddScheduledJobLogDetails`。
- 扩展定时任务执行结果，可以携带多条详情。
- 扩展仓储，执行日志和详情在同一次 `SaveChanges` 中写入。
- 新增详情查询接口。
- 文件存储一致性检查任务会为缺失文件或检查异常写入详情。
- Vben 定时任务日志抽屉新增 `详情` 操作，打开详情抽屉查看异常对象。

## 新增接口

```text
GET /system/scheduled-job/logs/{logId}/details
```

权限：

```text
system:scheduled-job:query
```

## 详情字段

- `DetailType`：详情类型，例如 `StorageMissing`、`StorageCheckError`。
- `TargetType`：对象类型，本次为 `ManagedFile`。
- `TargetId`：对象 ID。
- `TargetName`：对象名称。
- `StorageProvider`：存储方式，例如 `local`、`minio`。
- `StoragePath`：存储路径。
- `Status`：详情状态。
- `Message`：异常原因。

## 验证结果

红灯测试：

```text
GET /system/scheduled-job/logs/{logId}/details 返回 404
```

过滤测试：

```text
dotnet test ... --filter "ScheduledJobLogDetails"
已通过! - 失败: 0，通过: 1，已跳过: 0，总计: 1
```

完整后端测试：

```text
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
已通过! - 失败: 0，通过: 76，已跳过: 0，总计: 76
```

前端构建：

```text
pnpm run build:antd
Tasks: 11 successful, 11 total
```

## 如何手动测试

1. 重启后端，让迁移生效。
2. 进入 `系统监控 > 定时任务`。
3. 执行 `检查文件存储一致性`。
4. 点击 `日志`。
5. 在某条日志后点击 `详情`。
6. 如果本次检查存在缺失文件或异常，会看到文件 ID、文件名、存储方式、存储路径和原因。

## 后续扩展

- 增加详情导出。
- 增加异常文件修复动作。
- 增加“只看异常详情”的过滤条件。
- 增加任务执行详情保留策略，避免明细表无限增长。
