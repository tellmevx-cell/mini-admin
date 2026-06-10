# 工作流催办与消息中心联动总结

## 已实现

- 后端新增审批催办能力：`RemindTaskAsync` 校验任务状态和用户归属后，写入 `Remind` 流程日志。
- 消息中心新增 `WorkflowRemind` 来源通知：每次催办使用动作日志 ID 作为 `SourceId`，支持重复催办产生独立消息。
- API 新增 `POST /workflow/task/{id}/remind`，权限允许发起审批或审批处理用户访问，业务归属由仓储再次校验。
- 前端审批中心在“我的申请”的 Pending 流程上显示“催办”按钮，确认后通知当前审批人。
- 流程详情时间线可显示“催办”动作。

## 验证

- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter WorkflowAppServiceTests` 通过，13 个测试全绿。
- `dotnet build MiniAdmin.slnx --no-restore` 通过。
- `pnpm run build:antd` 通过。
- `git diff --check` 通过，仅提示既有 CRLF/LF 换行警告。

## 注意

全量 `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj` 本次在 3 分钟内超时，未获得最终结果；已清理该命令遗留的测试进程。受影响的工作流测试和解决方案构建均已通过。

## 收口判断

催办基础链路已经满足当前使用：发起人可催办、审批人可收到消息、流程详情可追踪日志。消息模板、投递重试和个人订阅偏好已由后续消息中心能力覆盖；催办频控、复杂订阅规则和 SLA 超时提醒暂不做，避免继续增加低收益平台功能。
