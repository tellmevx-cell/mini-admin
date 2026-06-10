# 消息模板中心阶段总结

## 已完成

- 通知中心新增“模板配置”页签。
- 后端新增消息模板实体、仓储、渲染服务和 API。
- 系统告警站内信已接入模板渲染。
- 工作流站内信已接入模板渲染：
  - `WorkflowTask`
  - `WorkflowApprove`
  - `WorkflowReject`
  - `WorkflowWithdraw`
  - `WorkflowTransfer`
  - `WorkflowRemind`
  - `WorkflowCc`
- 模板缺失或停用时保留原始文案兜底。

## 工作流模板变量

- `instanceId`
- `instanceTitle`
- `definitionId`
- `definitionName`
- `businessKey`
- `formDataJson`
- `initiatorUserId`
- `initiatorUserName`
- `currentNodeId`
- `currentNodeName`
- `nodeId`
- `nodeName`
- `taskId`
- `taskStatus`
- `approverUserId`
- `approverUserName`
- `operatorUserId`
- `operatorUserName`
- `targetUserId`
- `targetUserName`
- `targetRealName`
- `comment`
- `category`
- `level`
- `sourceType`
- `sourceId`

## 收口建议

- 当前模板中心已经满足工作流和站内信基础使用，不再继续增加低收益能力。
- 邮件/Webhook 独立模板、模板变量强校验、富文本编辑器都暂缓，等真实外部通知运营需要出现后再做。
- 后续进入业务模块前，只需要按消息中心冒烟手册验证工作流通知、抄送已读和投递记录即可。
