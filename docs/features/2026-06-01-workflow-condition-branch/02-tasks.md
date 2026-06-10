# 工作流条件分支任务执行文档

## 任务拆解

- [x] 为工作流测试补充条件分支用例。
- [x] 后端图执行支持穿透非审批节点。
- [x] 后端支持条件节点出口线判断。
- [x] 后端支持默认分支。
- [x] 前端连线支持保存条件字段、运算符、比较值、默认分支。
- [x] 前端右侧属性面板支持点击连线后编辑分支条件。
- [x] 保存前校验条件分支配置是否完整。
- [x] 前端构建验证。
- [x] 后端工作流专项测试验证。

## 后端实现

- 文件：
  - `src/MiniAdmin.Infrastructure/Persistence/EfWorkflowRepository.cs`
  - `tests/MiniAdmin.Tests/WorkflowAppServiceTests.cs`
- 核心变化：
  - `GetFirstExecutableNode` 和 `GetNextExecutableNode` 读取表单 JSON。
  - 新增递归查找下一可执行审批节点能力。
  - 条件节点按出口线条件选择目标节点。
  - 如果条件不命中，优先走默认分支。

## 前端实现

- 文件：
  - `frontend/vue-vben-admin/apps/web-antd/src/views/workflow/center/index.vue`
- 核心变化：
  - 支持点击连线选择分支。
  - 右侧面板新增“连线属性”。
  - 条件节点出口线可配置：
    - 分支名称。
    - 默认分支。
    - 字段路径。
    - 运算符。
    - 比较值。
  - 保存时将条件配置写入 `DesignerJson.edges`。
