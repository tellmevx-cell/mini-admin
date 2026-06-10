# 旧租户管理入口清理总结

## 完成内容

本次修复把历史遗留的 `系统管理 / 租户管理` 占位入口做了一次性清理。清理方式不是物理删除菜单，而是禁用、隐藏并移除角色授权，这样既不会误导用户点击无功能页面，也保留了历史数据记录。

真实租户管理入口保持为：

- 菜单：`平台管理 / 租户管理`
- 前端：`/platform/tenant`
- 后端：`/platform/tenant/*`

## 关键点

- 新增数据种子版本：`202605290004-legacy-system-tenant-management-cleanup`
- 清理目标：`TenantManagement`，路径 `/system/tenant`
- 回归测试：`DatabaseInitializer_Removes_Legacy_SystemTenantManagement_Menu`

## 后续建议

后面继续做租户相关功能时，租户平台侧能力统一放到 `平台管理` 下；租户内业务配置放到租户自己的系统菜单下，避免平台能力和租户业务能力混在一起。
