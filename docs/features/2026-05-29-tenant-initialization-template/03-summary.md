# 租户初始化模板总结

## 实现内容

- 新增租户初始化追踪字段：
  - 初始化模板编码
  - 初始化状态
  - 初始化时间
  - 初始化错误
- 新增内置模板 `standard` 标准企业模板。
- 新增模板选项接口：`GET /platform/tenant/initialization-templates`。
- 新增租户时自动初始化基础数据：
  - 部门：总部、研发部、市场部
  - 岗位：部门负责人、开发工程师、销售经理
  - 角色：普通员工
  - 普通员工默认拥有基础查询类菜单权限
  - 租户管理员默认归属总部和部门负责人岗位
- 租户列表增加初始化状态展示。
- 新增租户弹窗增加初始化模板选择。
- 修正部门、岗位、角色在平台上下文下的查询边界：平台系统管理只看平台基础数据，租户系统管理只看本租户基础数据，避免租户模板数据污染平台管理操作。

## 验证结果

- `dotnet test C:\tmp\mini-admin-tenant-init-template\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter Tenant_Create_Initializes_Standard_Template_Foundation_Data`
  - 结果：1 passed
- `dotnet test C:\tmp\mini-admin-tenant-init-template\MiniAdmin.slnx`
  - 结果：119 passed
- `pnpm run build:antd`
  - 结果：构建成功
- 本地服务：
  - 后端：`http://localhost:5320/health` 返回 Healthy
  - 前端：`http://localhost:5666/` 返回 200
- 真实接口抽查：
  - `/platform/tenant/initialization-templates`
  - 返回 `standard / 标准企业模板`

## 后续建议

- 下一阶段可以把模板从内置代码升级为可视化管理。
- 可增加“重新初始化”动作，但需要严格做到幂等并记录操作审计。
- 可在租户详情页展示初始化明细，例如创建了哪些部门、岗位、角色和菜单授权。
