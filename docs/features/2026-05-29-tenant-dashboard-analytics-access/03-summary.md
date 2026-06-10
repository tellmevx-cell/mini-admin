# 租户默认分析页访问修复总结

## 完成内容

本次修复解决了租户用户登录成功后落到 `/analytics` 但页面 404 的问题。根因是 Vben 默认首页和后台 `Dashboard` 重定向都指向 `/analytics`，但租户默认授权只包含 `Workspace`。

修复后：

- 新租户管理员默认拥有 `Analytics`。
- 默认套餐会包含 `Analytics`。
- 历史数据库里已有 `Dashboard` 但缺少 `Analytics` 的套餐和角色，会通过一次性种子自动补齐。
- 受影响租户用户授权缓存会被清理。

## 关键版本

- 数据种子版本：`202605290005-tenant-dashboard-analytics-access`
- 回归测试：`TenantAdmin_CreateTenant_Creates_Admin_And_Allows_Login`

## 设计说明

套餐仍然是租户可用功能上限，角色仍然是用户实际权限来源。本次只是修正默认首页和默认授权之间的不一致，不改变 RBAC 模型。
