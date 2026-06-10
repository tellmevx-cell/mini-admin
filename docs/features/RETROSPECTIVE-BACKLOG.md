# 历史功能文档回补清单

这份清单用于追踪“先开发、后补文档”的历史功能。回补文档会尽量依据现有代码、测试、计划文档和已完成结果整理，但细节精度会低于未来“先写需求再实现”的新功能。

## 已按新结构补齐

- [x] `2026-05-27-audit-entity-changes` 实体变更审计
- [x] `2026-05-27-permission-diagnostics` 权限诊断中心
- [x] `2026-05-26-project-setup` 项目初始化与分层
- [x] `2026-05-26-official-vben-login-loop` 官方 Vben 对接闭环
- [x] `2026-05-26-mysql-persistence` MySQL 持久化
- [x] `2026-05-26-jwt-authentication` JWT 登录认证
- [x] `2026-05-27-login-security` 登录安全与账号锁定
- [x] `2026-05-27-rbac-permissions` RBAC 角色、菜单、按钮权限
- [x] `2026-05-27-user-department-tree` 用户管理与部门树筛选
- [x] `2026-05-27-data-scope` 数据权限
- [x] `2026-05-27-menu-crud` 菜单管理 CRUD
- [x] `2026-05-27-department-management` 部门管理
- [x] `2026-05-27-position-management` 岗位管理
- [x] `2026-05-27-dictionary-management` 字典管理
- [x] `2026-05-27-parameter-settings` 参数设置
- [x] `2026-05-27-notice-announcement` 通知公告
- [x] `2026-05-27-audit-log-basic` 审计日志基础能力
- [x] `2026-05-27-file-storage` 文件存储：本地与 MinIO
- [x] `2026-05-27-online-users` 在线用户与强制下线
- [x] `2026-05-27-redis-authorization-cache` Redis 授权缓存
- [x] `2026-05-27-password-management` 密码修改与重置

## 待回补

暂无。

## 回补原则

- 不记录真实数据库、Redis、MinIO 密钥。
- 老功能文档标注为“回补整理”。
- 有数据流、权限流、请求链路时补 Mermaid 图。
- 以后新功能不再回补，而是在开工前先创建需求文档。
