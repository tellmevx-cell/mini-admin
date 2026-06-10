# 日志管理与系统监控菜单重整总结文档

## 本次完成内容

本阶段把日志类和运行监控类功能从“系统管理”中拆出来，形成更清晰的后台菜单信息架构。

新的一级菜单：

- `日志管理`
- `系统监控`

新的结构：

```text
系统管理
├─ 租户套餐
├─ 租户管理
├─ 用户管理
├─ 文件管理
├─ 角色管理
├─ 菜单管理
├─ 部门管理
├─ 岗位管理
├─ 字典管理
├─ 参数设置
├─ 通知公告

日志管理
├─ 操作日志
├─ 登录日志

系统监控
├─ 在线用户
├─ 权限诊断
```

## 实现方式

新增 seed version：

```text
202605280002-menu-monitor-log-restructure
```

它负责：

- 新增一级菜单 `LogManagement`，显示为“日志管理”。
- 将旧的 `LogManagementMenuId` 改为 `OperationLog`，显示为“操作日志”。
- 登录日志移动到日志管理下面。
- 新增一级菜单 `SystemMonitor`，显示为“系统监控”。
- 在线用户移动到系统监控下面。
- 权限诊断移动到系统监控下面。
- 如果角色已经拥有相关子菜单授权，自动补齐新父菜单授权。

## 权限兼容

本次没有修改已有权限码：

- `system:log:query`
- `system:log:export`
- `system:login-log:query`
- `system:online-user:query`
- `system:online-user:force-logout`
- `system:permission-diagnostics:query`
- `system:permission-diagnostics:refresh-cache`

页面路径也保持不变：

- 操作日志：`/system/log`
- 登录日志：`/system/login-log`
- 在线用户：`/system/online-user`
- 权限诊断：`/system/permission-diagnostics`

## 后续衔接

下一阶段可以在 `系统监控` 下新增：

```text
系统监控
├─ 在线用户
├─ 定时任务
├─ 权限诊断
```

定时任务中心的第一个内置任务建议做“清理 90 天前审计日志”，把现在启动时执行的清理逻辑迁到任务调度里。

## 验证结果

- 菜单结构测试：2 个测试通过。
- 后端完整测试：70 个测试通过。
- 后端启动健康检查：`MiniAdmin.Api Healthy`。

## 使用提醒

菜单和权限在前端、Redis/内存缓存里都有缓存。后端启动时已经清理了初始化用户的授权缓存；如果浏览器里仍显示旧菜单，退出重新登录一次即可。
