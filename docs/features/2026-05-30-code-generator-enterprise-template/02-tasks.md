# 代码生成器企业级模板增强任务执行文档

## 任务清单

- [x] 扩展代码生成请求模型：`DataScopeMode`、`DataScopeField`、`EnableAudit`
- [x] 增加后端失败测试，锁定数据权限模板输出
- [x] 修改模板校验，启用数据权限时必须选择有效字段
- [x] 修改后端模板渲染，生成数据权限过滤代码
- [x] 修改安装指引，展示租户、数据权限、审计步骤
- [x] 修改前端 API 类型和配置页面
- [x] 补充完工总结
- [x] 运行测试、前端检查和构建

## 实施顺序

1. 先写测试验证 `Department` 模式生成内容。
2. 扩展 DTO，但保持默认值，兼容旧历史记录。
3. 修改 `CodeGeneratorTemplateRenderer`，只在启用数据权限时生成额外代码。
4. 修改 `CodeGeneratorAppService` 校验和安装指引。
5. 修改 Vben 页面配置项。
6. 跑 `dotnet test --filter CodeGenerator` 和 `pnpm run build:antd`。

## 风险

- 生成代码字符串很长，修改时要保持缩进和 C# 编译正确。
- 数据权限字段必须是实体字段，否则生成的代码不可编译。
- 列表、编辑、删除要一致接入数据权限，不能只保护列表。
