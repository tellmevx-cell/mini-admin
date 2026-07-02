# 功能截图展示

本页用于给 GitHub 访客和二开团队快速了解 MiniAdmin 的实际界面。截图由本地自动化脚本生成，脚本位于 `scripts/capture-feature-screenshots.mjs`。

运行方式：

```powershell
pnpm screenshots:features
```

默认要求：

- 后端运行在 `http://localhost:5021`。
- 前端运行在 `http://localhost:5666`。
- 默认登录账号为 `admin / 123456`。

如果你的地址不同，可以通过环境变量覆盖：

```powershell
$env:MINIADMIN_WEB_URL = "http://localhost:5600"
$env:MINIADMIN_USERNAME = "admin"
$env:MINIADMIN_PASSWORD = "123456"
pnpm screenshots:features
```

## 访问入口

### 登录入口

平台管理员和演示租户共用的登录入口，适合 SaaS 多租户后台的基础登录场景。

![登录入口](/screenshots/features/00-login.png)

## 工作台

### 分析页

后台首页提供系统概览和快捷入口，用于进入日常运维、审批和业务处理。

![分析页](/screenshots/features/01-dashboard.png)

## 工作流与消息

### 审批中心

审批中心集中承载待办、申请、抄送、流程实例、流程定义和业务绑定。

![审批中心](/screenshots/features/02-workflow-center.png)

### 消息通知中心

消息中心覆盖站内信、通知模板、通知策略、个人订阅和投递记录。

![消息通知中心](/screenshots/features/03-message-center.png)

## SaaS 多租户

### 租户管理

平台侧可以维护租户、租户状态、套餐授权和初始化模板。

![租户管理](/screenshots/features/04-tenant.png)

## 认证与 RBAC

### 用户管理

用户管理覆盖用户资料、部门岗位、角色分配、启停状态和导入导出。

![用户管理](/screenshots/features/05-user.png)

### 角色管理

角色管理用于配置菜单权限、按钮权限和数据权限边界。

![角色管理](/screenshots/features/06-role.png)

### 菜单管理

菜单管理维护 Vben 动态路由、菜单层级、组件路径和权限码。

![菜单管理](/screenshots/features/07-menu.png)

### 权限诊断

权限诊断用于排查用户、角色、菜单、数据范围和缓存授权链路。

![权限诊断](/screenshots/features/08-permission-diagnostics.png)

## 代码生成与业务示例

### 代码生成器

代码生成器支持表结构读取、字段选择、生成预览、安装、历史和回滚。

![代码生成器](/screenshots/features/09-code-generator.png)

### 示例订单

示例订单展示普通业务模块如何接入工作流审批。

![示例订单](/screenshots/features/10-sample-order.png)

### 客户资料

客户资料是标准 CRUD 示例，可作为二开业务模块参考。

![客户资料](/screenshots/features/11-customer.png)

## 文件存储

### 文件管理

文件管理提供上传、下载、本地存储和 MinIO 扩展入口。

![文件管理](/screenshots/features/12-file-storage.png)

## 监控与审计

### 系统监控

系统监控展示运行指标、健康状态和系统资源。

![系统监控](/screenshots/features/13-monitor.png)

### 告警中心

告警中心展示告警列表、告警规则和通知联动。

![告警中心](/screenshots/features/14-alert-center.png)

### 操作日志

操作日志记录接口审计、请求记录、实体变更和导出能力。

![操作日志](/screenshots/features/15-audit-log.png)

## 安全中心

### 登录日志

登录日志用于追踪登录成功、失败、锁定和来源信息。

![登录日志](/screenshots/features/16-login-log.png)

### 安全中心

安全中心集中维护登录安全策略、密码策略、安全事件和在线会话。

![安全中心](/screenshots/features/17-security-center.png)

## 工程化

### 项目运行管理

项目运行管理用于查看本地服务、运行日志、构建记录和构建产物。

![项目运行管理](/screenshots/features/18-project-runtime.png)

### 定时任务

定时任务提供任务配置、手动执行、任务日志和运行状态。

![定时任务](/screenshots/features/19-scheduled-job.png)
