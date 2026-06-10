# 租户默认分析页访问修复需求

## 背景

租户用户登录后，Vben 默认首页会进入 `/analytics`。后台菜单中 `Dashboard` 的默认重定向也是 `/analytics`。

之前租户默认套餐和租户管理员默认菜单只包含 `Dashboard` 与 `Workspace`，没有包含 `Analytics`。因此租户用户虽然登录成功，但进入 `/analytics` 时前端找不到已授权路由，显示 404。

## 目标

- 租户用户默认可访问 Vben 登录后的默认首页 `/analytics`。
- 新建租户管理员默认菜单包含 `Analytics`。
- 旧数据库中的默认套餐和已有角色，如果已经有 `Dashboard`，自动补齐 `Analytics`。
- 清理受影响租户用户菜单缓存，避免登录后继续看到旧菜单。

## 非目标

- 不改变套餐作为权限上限、角色作为实际授权的 RBAC 设计。
- 不绕过租户套餐限制。
- 不修改 Vben 官方默认首页配置。

## 权限流转

```mermaid
flowchart LR
    A["Vben 登录成功"] --> B["进入默认首页 /analytics"]
    B --> C["后端 /menu/all 返回授权路由"]
    C --> D["租户套餐允许 Analytics"]
    D --> E["角色菜单包含 Analytics"]
    E --> F["前端正常渲染分析页"]
```
