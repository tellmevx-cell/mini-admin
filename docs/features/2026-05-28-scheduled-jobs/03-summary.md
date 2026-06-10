# 定时任务中心总结文档

## 本次完成

- 新增 `系统监控 > 定时任务` 菜单。
- 新增任务列表、编辑、启停、手动执行和执行日志能力。
- 新增后台 `ScheduledJobWorker`，按 `IntervalSeconds` 和 `NextRunAt` 调度启用任务。
- 新增内置任务 `audit-log-cleanup`，用于清理 90 天前的操作审计日志。
- 新增执行日志表，记录每次任务的开始时间、结束时间、耗时、结果和错误信息。
- 前端新增 Vben 页面，可以查询任务、编辑配置、手动运行、查看执行日志。

## 后端实现

新增实体：

- `ScheduledJob`：保存任务配置。
- `ScheduledJobLog`：保存任务执行记录。

新增接口：

- `GET /system/scheduled-job/list`
- `PUT /system/scheduled-job/{id}`
- `POST /system/scheduled-job/{id}/run`
- `GET /system/scheduled-job/{id}/logs`

权限点：

- `system:scheduled-job:query`
- `system:scheduled-job:update`
- `system:scheduled-job:run`

Seed 版本：

```text
202605280003-scheduled-jobs
```

迁移：

```text
20260528025314_AddScheduledJobs
```

## 前端实现

新增 API：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/scheduled-job.ts
```

新增页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/scheduled-job/index.vue
```

页面能力：

- 按任务名称、任务 Key、启用状态查询。
- 编辑任务名称、描述、间隔和启用状态。
- 手动执行一次任务。
- 打开抽屉查看任务执行日志。
- 根据按钮权限控制编辑和执行入口。

## 验证结果

后端完整测试：

```text
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
已通过! - 失败: 0，通过: 73，已跳过: 0，总计: 73
```

前端构建：

```text
pnpm run build:antd
Tasks: 11 successful, 11 total
```

## 如何手动测试

1. 重新启动后端，让迁移和 seed 生效。
2. 重新登录前端，刷新菜单和按钮权限缓存。
3. 进入 `系统监控 > 定时任务`。
4. 确认列表中存在 `audit-log-cleanup`。
5. 点击 `执行`，确认提示成功。
6. 点击 `日志`，确认出现一条执行记录。
7. 修改任务间隔或启用状态，保存后刷新列表确认生效。

## 后续扩展

- 增加 cron 表达式调度。
- 增加分布式锁，支持多实例部署。
- 增加任务参数配置。
- 增加任务失败告警。
- 增加更多内置任务，例如在线用户会话清理、临时文件清理、文件存储一致性检查。
