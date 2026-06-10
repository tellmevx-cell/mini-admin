# 前端开发

MiniAdmin 前端基于 Vben Admin 的 Ant Design Vue 应用。

## 目录入口

```text
frontend/vue-vben-admin/apps/web-antd/src
```

常用目录：

| 目录 | 用途 |
| --- | --- |
| `api` | 请求函数和 TS 类型 |
| `views` | 页面 |
| `router` | 路由接入 |
| `store` | 状态 |
| `components` | 组件 |

## 新增页面流程

1. 在 `api` 下新增接口文件。
2. 在 `views` 下新增页面目录。
3. 后端种子数据新增菜单和权限。
4. 页面根据权限控制按钮显示。
5. 登录后确认动态菜单可打开页面。

## API 封装建议

前端 API 文件应包含：

- 请求类型。
- 响应类型。
- 查询函数。
- 创建、更新、删除或动作函数。

不要在页面中散落 URL 字符串。

## 页面交互建议

后台页面优先保证：

- 列表筛选清晰。
- 操作按钮权限明确。
- 表单校验完整。
- 弹窗或抽屉关闭后刷新必要数据。
- 错误提示可理解。

## 权限按钮

按钮权限要和后端权限编码一致。

例如：

```text
system:user:create
system:user:update
system:user:delete
```

前端只负责隐藏和提示，后端仍必须做授权校验。

## 验证命令

类型检查：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

构建：

```powershell
pnpm -F @vben/web-antd build
```
