# 官方 Vben 对接闭环需求文档

## 背景

前端要求使用官方 Vben，而不是直接使用 yiabp-mini 提供的前端。需要完成官方 Vben 拉取、启动、登录接口适配和菜单权限对接。

## 目标

- 使用官方 Vben 项目作为前端基础。
- 对接 MiniAdmin 后端登录。
- `/user/info`、`/auth/codes`、`/menu/all` 能支撑 Vben 动态权限。
- 刷新页面不会因为旧 token 陷入登录跳转问题。

## 功能范围

- 前端接口适配。
- 登录响应格式适配。
- 用户信息格式适配。
- 权限码与动态菜单适配。

## 不做范围

- 不直接复用 yiabp-mini 前端。
- 不做完整 UI 重设计。

## 数据流转

```mermaid
flowchart LR
    A["Vben 登录页"] --> B["POST /auth/login"]
    B --> C["MiniAdmin 登录服务"]
    C --> D["返回 accessToken"]
    D --> E["GET /user/info"]
    E --> F["GET /auth/codes"]
    F --> G["GET /menu/all"]
    G --> H["Vben 生成动态菜单和路由"]
```

## 验收标准

- [x] 前端可以看到登录效果。
- [x] 登录后进入后台。
- [x] 菜单来自后端。
- [x] 刷新后可以正确进入登录页或后台。
