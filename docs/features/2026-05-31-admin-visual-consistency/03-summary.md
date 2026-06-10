# 全后台视觉一致性整理总结

## 完成内容

- 新增全局视觉一致性样式层：
  - `frontend/vue-vben-admin/apps/web-antd/src/styles/mini-admin-polish.css`
- 在前端启动入口引入该样式层：
  - `frontend/vue-vben-admin/apps/web-antd/src/bootstrap.ts`
- 覆盖后台常见表格页、配置页、工作台页的容器、工具栏、表格、弹窗、抽屉和移动端折行细节。

## 影响页面

该样式层会影响所有使用 Ant Design 表格、Modal、Drawer，以及常见类名 `table-shell`、`table-toolbar`、`panel`、`info-panel` 的页面，包括但不限于：

- 用户管理
- 角色管理
- 菜单管理
- 部门管理
- 岗位管理
- 字典管理
- 参数设置
- 通知公告
- 文件管理
- 登录日志
- 在线用户
- 租户管理
- 租户套餐
- 业务客户和订单页面
- 代码生成器
- 系统监控
- 项目运行管理

## 验证项

- `npx impeccable --json frontend/vue-vben-admin/apps/web-antd/src/views`
- `pnpm run build:antd`

## 后续建议

- 后续新增页面尽量复用 `table-shell`、`table-toolbar`、`panel` 等结构类名。
- 如果某个页面有特殊布局，再用页面 scoped style 做局部增强。
