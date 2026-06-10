# 官方 Vben 对接闭环任务执行文档

## 任务清单

- [x] 拉取官方 Vben
- [x] 启动 `web-antd`
- [x] 配置 API 地址
- [x] 适配登录接口
- [x] 适配用户信息接口
- [x] 适配权限码接口
- [x] 适配菜单接口
- [x] 修复旧 token 刷新无法进入登录页问题

## 涉及文件

- `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/router/guard.ts`
- `frontend/vue-vben-admin/packages/types/src/user.ts`
- `src/MiniAdmin.Api/Program.cs`

## 执行步骤

1. 启动官方 Vben。
2. 对接后端认证接口。
3. 返回符合 Vben 预期的用户信息、权限码和菜单。
4. 调整路由守卫，避免旧 token 强制跳离登录页。

## 验证命令

```powershell
pnpm run dev:antd
```

```powershell
Invoke-WebRequest -Uri http://localhost:5666/login -UseBasicParsing
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成，本文档为回补整理。
