# Workflow CC Node Execution Summary

## 本次完成

- `WorkflowNode` 增加 `NodeType`，支持 `approve` 和 `cc`。
- `SaveWorkflowNodeRequest` 和 `WorkflowNodeDto` 增加节点类型字段。
- EF Core 映射增加 `NodeType`。
- MySQL 初始化增加自动补列逻辑：`mini_workflow_nodes.NodeType`，默认 `approve`。
- 流程定义保存时持久化节点类型。
- 流程流转经过抄送节点时：
  - 解析指定用户或指定角色接收人。
  - 写入 `WorkflowActionLog`，`Action = Cc`。
  - 不生成待办任务。
  - 自动继续寻找下一个审批节点或结束节点。
- 前端设计器新增抄送节点执行态配置：
  - 抄送节点进入节点属性面板。
  - 支持配置接收类型和接收人。
  - 保存时提交 `nodeType`。
- 请假流程示例中的“抄送人事”变成真实执行节点。

## 验证

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Workflow"`
  - 通过，18/18。
- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`
  - 通过，0 警告，0 错误。
- 前端 Vite production build
  - 通过。
- 后端启动成功，`GET http://localhost:5021/health`
  - 返回 `Healthy`。
- 登录后请求 `GET /workflow/definition/list`
  - 可返回节点 `nodeType`。
- 登录后请求 `GET /workflow/instance/cc`
  - 接口可访问。

## 使用方式

1. 进入审批中心的流程定义。
2. 在画布中添加“抄送节点”。
3. 选中抄送节点，在右侧配置接收类型和接收人。
4. 保存并发布流程。
5. 发起流程后，流程经过抄送节点会写入抄送日志。
6. 被抄送人登录后，在“我的抄送”页签查看。

## 后续建议

- 增加抄送已读/未读状态。
- 抄送写入后联动站内信通知。
- 流程详情中把抄送记录按“抄送给谁”独立分组展示。
