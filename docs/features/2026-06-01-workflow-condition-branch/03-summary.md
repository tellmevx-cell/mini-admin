# 工作流条件分支总结文档

## 已完成

- 条件节点从“设计态”升级为“可执行分支节点”。
- 后端根据发起表单 JSON 判断条件并选择分支。
- 支持默认分支。
- 支持穿透条件、抄送等非审批节点，继续寻找下一审批节点或结束节点。
- 前端支持点击连线编辑分支条件。
- 保存前会检查条件分支是否配置完整。

## 验证结果

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter FullyQualifiedName~WorkflowAppServiceTests --logger "console;verbosity=minimal"`：通过，2 个工作流测试全部通过。
- `pnpm run build:antd`：通过。

## 使用方式

1. 新增或编辑流程定义。
2. 在审批节点后新增条件节点。
3. 从条件节点连到不同目标节点。
4. 点击条件节点的出口线。
5. 在右侧“连线属性”配置字段路径、运算符、比较值。
6. 给兜底出口线开启“默认分支”。
7. 发起流程时，在表单 JSON 中提供对应字段。

## 后续建议

- 条件节点增加分支列表视图，不依赖点击细线。
- 支持多条件组合：全部满足、任一满足。
- 条件表达式支持字段类型提示和示例值测试。
- 抄送节点接入通知中心。
