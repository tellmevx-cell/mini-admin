# 工作流业务绑定配置总结

## 本次完成

- 新增 `WorkflowBusinessBinding` 领域实体，用于维护业务类型和流程定义的绑定关系。
- 后端支持业务绑定列表、新增、编辑、删除，以及按业务类型解析当前可用流程。
- 保存绑定时会校验流程定义必须是已发布且启用状态。
- 同一租户下同一个业务类型只能绑定一次，重复保存会被拒绝。
- 停用绑定后，按业务类型解析流程时返回空。
- MySQL 初始化逻辑新增 `mini_workflow_business_bindings` 表。
- 审批中心新增“业务绑定”页签，支持搜索、启停用筛选、新增、编辑、删除和流程版本选择。

## 关键接口

- `GET /workflow/business-binding/list`：分页查询业务绑定。
- `POST /workflow/business-binding`：新增业务绑定。
- `PUT /workflow/business-binding/{id}`：编辑业务绑定。
- `DELETE /workflow/business-binding/{id}`：删除业务绑定。
- `GET /workflow/business-binding/resolve/{businessType}`：按业务类型解析已启用绑定流程。

## 验证结果

- 后端工作流相关测试通过：12 个通过，0 个失败。
- 后端构建通过：0 个警告，0 个错误。
- 前端 `pnpm run build:antd` 构建通过。
- 运行时接口闭环验证通过：
  - 创建流程草稿。
  - 发布流程定义。
  - 新增业务类型绑定。
  - 按业务类型解析出绑定流程和版本。

## 启动状态

- 后端已启动：`http://localhost:5320`
- 前端已启动：`http://localhost:5666`
- 本次仍使用 `InMemory + Memory Cache` 启动，原因是当前 MySQL 连接存在 SSL 认证问题。

## 后续建议

下一步建议做“代码生成器接入工作流绑定”：在生成业务模块时增加“启用审批”选项，自动生成审批状态字段、提交审批、撤回审批、查看流程记录等代码。
