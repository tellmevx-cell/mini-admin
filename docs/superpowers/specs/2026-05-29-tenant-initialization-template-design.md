# 租户初始化模板设计

## 范围

本阶段实现“租户开通闭环”的第一版：新增租户时选择内置初始化模板，系统自动创建租户基础组织、岗位、普通员工角色，并记录初始化状态。模板先以内置代码形式落地，后续再扩展为数据库可视化管理。

## 设计决策

- 使用 `standard` 作为默认模板编码。
- 租户实体增加初始化追踪字段：
  - `InitializationTemplateCode`
  - `InitializationStatus`
  - `InitializedAt`
  - `InitializationError`
- 初始化状态使用字符串保存，第一版取值为 `Pending`、`Success`、`Failed`。
- 标准模板创建租户范围内的数据，不创建平台级数据。
- 租户管理员默认归属总部和部门负责人岗位。
- 普通员工角色只分配基础查询类权限，避免默认拥有危险操作权限。

## 后端结构

- `CreateTenantRequest` 增加 `InitializationTemplateCode`。
- `TenantDto` 返回初始化字段。
- 新增 `TenantInitializationTemplateDto` 给前端选择模板。
- 新增 `TenantInitializationTemplateService` 负责按模板初始化租户基础数据。
- `EfTenantRepository.CreateAsync` 负责事务边界：租户、管理员、初始化数据一起保存，失败则整体回滚。

## 前端结构

- 租户 API 增加模板选项接口类型。
- 租户新增弹窗增加初始化模板下拉框。
- 租户列表增加初始化状态展示。

## 测试策略

- 后端新增集成测试：创建租户后验证租户初始化状态、部门、岗位、普通员工角色、角色菜单。
- 保留现有租户登录和菜单测试，确认初始化模板不会破坏租户管理员登录。
- 前端通过 `pnpm run build:antd` 验证类型和模板编译。
