# 消息通知中心总结文档

## 本次完成内容

- 将原有 `通知中心` 升级为 `消息通知中心`。
- 新增消息通道概览接口：`GET /notification/channels/overview`。
- 新增消息投递记录接口：`GET /notification/deliveries`。
- 新增 Webhook 实际投递器：工作流策略开启 Webhook 后会向配置地址 POST JSON，并记录成功、失败或跳过状态。
- 新增投递记录手工重发接口：`POST /notification/deliveries/{id}/retry`，支持邮件与 Webhook 失败/跳过记录重新投递。
- 新增投递失败站内告警：邮件/Webhook 投递失败或跳过时，会给 `admin` 角色生成 `NotificationDeliveryFailure` 告警消息，并跳转到投递记录页定位失败原因。
- 新增通知投递自动重试定时任务：`notification-delivery-retry`，后台扫描失败/跳过的邮件与 Webhook 投递记录并批量重试。
- 新增个人订阅偏好：用户可以按通知事件选择是否接收站内信、邮件和 Webhook。
- 新增订阅偏好批量恢复默认：当前用户可以一键清除全部自定义偏好，重新跟随全局通知策略。
- 工作流关键动作已接入站内信：
  - 发起审批通知审批人
  - 审批通过通知发起人
  - 审批驳回通知发起人
  - 撤回审批通知发起人
  - 转办通知新处理人
  - 抄送节点通知被抄送人
- 工作流抄送已升级为可追踪阅知状态：
  - 抄送节点落独立 `mini_workflow_cc_records` 记录
  - 我的抄送支持未读/已读筛选
  - 打开抄送详情或点击标为已读会回写阅读时间
  - 工作流通知链接携带 `workflowCcId` 时可打开详情并自动标记已读
  - 流程详情 Drawer 支持查看抄送回执，展示每个抄送人的已读/未读和阅读时间
- 新增上线前冒烟测试手册：`docs/features/2026-06-03-message-notification-hub/04-smoke-runbook.md`，覆盖本地启动、测试点、通知配置和常见排障。
- 前端 `/system/notification` 页面升级为三段式工作台：
  - 概览指标
  - 我的消息
  - 投递记录
  - 通知策略
  - 订阅偏好
  - 模板配置
  - 通道概览

## 关键实现点

### 1. 工作流通知不新建表

继续复用现有 `mini_user_notifications`，通过不同 `SourceType` 区分工作流消息：

- `WorkflowTask`
- `WorkflowApprove`
- `WorkflowReject`
- `WorkflowWithdraw`
- `WorkflowTransfer`
- `WorkflowCc`

这样可以延续现有顶部铃铛、未读数和个人消息列表，不需要额外改消息基础设施。

### 2. 投递记录复用既有模型

继续复用 `mini_notification_deliveries` 作为通道投递记录表。邮件和 Webhook 都会落同一张表，方便统一筛选、排查和手工重发。

### 3. 页面结构做成可继续扩展的工作台

前端没有在原列表页上继续堆按钮，而是调整为：

- 顶部概览指标
- 中部页签工作区
- 底部通道说明

后续如果要接入：

- 自动重试策略参数配置
- 模板管理
- 消息订阅规则

都可以在这个结构上继续扩展。

### 4. 自动重试复用定时任务体系

自动重试没有新增独立页面，而是作为内置定时任务进入 `系统监控 / 定时任务`：

- 任务编码：`notification-delivery-retry`
- 默认间隔：10 分钟
- 单批处理：最多 50 条
- 重试上限：`RetryCount < 3`

这样既能等待后台 Worker 自动执行，也能在定时任务页面手动运行，方便联调和排障。

### 5. 个人订阅偏好覆盖全局策略

全局通知策略仍然定义事件是否启用、默认打开哪些通道；个人订阅偏好只作用于当前登录用户：

- 未设置个人偏好时，自动跟随全局策略。
- 设置个人偏好后，可以关闭某个事件或某个通道。
- 支持一键恢复全部默认，批量清除当前用户的自定义订阅偏好。
- 全局策略已关闭的通道，个人偏好不能强制打开。
- 工作流通知发送时会先计算全局策略，再叠加个人偏好。

## 主要修改文件

- 后端 API：`src/MiniAdmin.Api/Program.cs`
- 工作流通知：`src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`
- 工作流抄送记录：`src/MiniAdmin.Domain/Entities/WorkflowCcRecord.cs`
- 投递记录服务：`src/MiniAdmin.Infrastructure/Notifications/NotificationDeliveryService.cs`
- 定时任务执行器：`src/MiniAdmin.Infrastructure/ScheduledJobs/ScheduledJobExecutor.cs`
- 默认种子：`src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- 订阅偏好：`src/MiniAdmin.Application/UserNotifications/NotificationSubscriptionAppService.cs`
- 通知契约：`src/MiniAdmin.Application.Contracts/UserNotifications/UserNotificationDtos.cs`
- 前端 API：`frontend/vue-vben-admin/apps/web-antd/src/api/core/notification.ts`
- 前端页面：`frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`
- 测试：
  - `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`
  - `tests/MiniAdmin.Tests/NotificationDeliveryServiceTests.cs`
  - `tests/MiniAdmin.Tests/NotificationPolicyAppServiceTests.cs`

## 验证结果

- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "FullyQualifiedName~WorkflowAppServiceTests|FullyQualifiedName~NotificationDeliveryServiceTests|FullyQualifiedName~NotificationPolicyAppServiceTests"`
  - 通过：57/57
- `pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false`
  - 通过
- `pnpm -F @vben/web-antd build`
  - 通过，已生成 `apps/web-antd/dist.zip`

## 当前运行状态

- 前端：`http://localhost:5666`
- 后端：`http://localhost:5021`

## 冒烟测试手册

- 手册路径：`docs/features/2026-06-03-message-notification-hub/04-smoke-runbook.md`
- 覆盖范围：请假审批发起、同意、驳回、抄送已读/未读、消息跳转、投递失败重试、订阅恢复默认。
- 使用建议：进入业务模块联调前，按手册跑一次即可，不再额外扩展非必要工作流能力。

## 收口建议

- 当前工作流与消息中心已经满足基础使用；暂不继续增加催读、加签、代理审批、统计大屏等扩展能力。
- 后续进入业务模块前，建议只保留一次真实环境冒烟：发起审批、审批通过/驳回、抄送已读、投递失败重试、订阅恢复默认。
