# 工作流催办与消息中心联动任务

- [x] 新增 `WorkflowRemindTaskRequest` 和 `RemindTaskAsync` 应用服务合同。
- [x] 新增工作流仓储催办实现。
- [x] 催办动作写入 `WorkflowActionLog`，动作类型为 `Remind`。
- [x] 催办消息写入 `UserNotifications`，消息来源为 `WorkflowRemind`。
- [x] 新增 `POST /workflow/task/{id}/remind` API。
- [x] 前端工作流 API 增加 `remindWorkflowTaskApi`。
- [x] “我的申请”待处理流程增加催办按钮。
- [x] 流程详情时间线展示“催办”动作标签。
- [x] 增加后端单元测试覆盖发起人催办和消息生成。
- [x] 运行工作流测试、后端构建、前端 `build:antd`。

## 后续处理结论

- 已覆盖：消息模板中心已经支持工作流事件的标题、正文和跳转链接配置。
- 已覆盖：邮件/Webhook 投递记录、失败/跳过重试和管理员告警已经纳入消息中心。
- 已覆盖：个人订阅偏好已经支持用户按事件关闭或恢复默认。
- 暂缓：按角色、组织、业务类型的复杂订阅规则收益不高，当前不新增。
- 暂缓：催办频控和工作流 SLA 超时提醒属于增强能力，当前基础审批可用，不新增。
