# 工作流定义版本管理与发布总结

## 本次完成

- 工作流定义增加 `Version`、`PublishStatus`、`PublishedAt`。
- 新建流程默认是 `Draft v1`，草稿可以编辑，但不能用于发起审批。
- 发布草稿后变为 `Published`，发起审批选项只展示已发布、启用、有可用节点的流程。
- 已发布且已经产生实例的流程不能直接编辑，需要先复制新版本。
- 从已发布流程可以复制新草稿版本，复制基础信息、设计器 JSON 和审批节点。
- 发布新版本后，同编码下旧的已发布版本自动变为 `Archived`。
- MySQL 初始化兼容逻辑会给旧库补齐版本字段，并调整唯一索引为 `TenantId + Code + Version`。
- 前端审批中心展示版本和发布状态，支持“保存草稿”“发布”“新版本”操作。

## 关键接口

- `POST /workflow/definition`：创建草稿。
- `PUT /workflow/definition/{id}`：保存草稿或允许编辑的定义。
- `POST /workflow/definition/{id}/publish`：发布流程定义。
- `POST /workflow/definition/{id}/new-version`：复制新版本草稿。
- `GET /workflow/definition/options`：获取可发起审批的已发布流程。

## 验证结果

- 后端工作流相关测试通过：9 个通过，0 个失败。
- 后端构建通过：0 个警告，0 个错误。
- 前端 `pnpm run build:antd` 构建通过。
- 接口闭环验证通过：
  - 创建草稿返回 `Draft v1`。
  - 发布后返回 `Published v1`。
  - 发布后流程出现在发起审批选项中。
  - 创建新版本返回 `Draft v2`。
  - 发布 v2 后，v1 自动归档为 `Archived`，v2 为 `Published`。

## 启动状态

- 后端已启动：`http://localhost:5320`
- 前端已启动：`http://localhost:5666`
- 本次后端使用 `InMemory + Memory Cache` 启动。

## 注意事项

当前开发环境按 MySQL 启动时出现 `SSL Authentication Error`，所以按约定切换到了内存模式启动。这个问题不是工作流版本功能引起的，后续如果要恢复线上 MySQL 验证，可以优先检查连接串 SSL 配置，例如显式加上 `SslMode=None` 或按云数据库要求配置证书。

## 后续建议

下一步可以继续做“工作流模板和业务表单绑定治理”：把示例订单审批经验沉淀成可配置模板，让代码生成器生成业务模块时可以选择是否绑定审批流程。
