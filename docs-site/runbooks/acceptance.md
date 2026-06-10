# 验收清单

完整验收手册见仓库内：

```text
docs/runbooks/workflow-message-center-acceptance-checklist.md
```

本页提供上线或交接前的高频检查项。

## 环境检查

- 后端 API 可启动。
- `/health` 返回成功。
- 前端可打开。
- 管理员可登录。
- 文档站可构建。

## 平台基础检查

- 用户管理可打开。
- 角色管理可打开。
- 菜单管理可打开。
- 权限诊断可使用。
- 租户管理可打开。
- 审计日志可查询。
- 系统监控可打开。

## 工作流检查

- 流程定义存在且启用。
- 发起审批成功。
- 我的申请能看到流程。
- 我的待办能看到任务。
- 同意成功。
- 驳回成功。
- 撤回成功。
- 转办成功。
- 催办能生成消息。
- 流程详情能展示表单、任务和流转记录。

## 抄送检查

- 抄送节点能生成抄送记录。
- 我的抄送能看到记录。
- 未读筛选有效。
- 打开详情后自动已读。
- 流程详情能看到抄送回执。

## 消息中心检查

- 顶部铃铛显示未读消息。
- 我的消息可筛选已读和未读。
- 工作流消息可跳转详情。
- 通知策略可保存。
- 订阅偏好可保存和恢复默认。
- 模板配置可保存并渲染。
- 投递记录能显示失败、跳过或成功状态。

## 自动化命令

后端关键测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests|NotificationTemplateAppServiceTests|NotificationPolicyAppServiceTests|NotificationDeliveryServiceTests" --no-restore
```

前端类型检查：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

文档站构建：

```powershell
pnpm docs:build
```

## 放行标准

可以放行：

- 核心手工验收通过。
- 后端关键测试通过。
- 前端类型检查通过。
- 文档站构建通过。
- 没有高风险权限或数据隔离问题。

不建议放行：

- 登录失败。
- 菜单权限异常。
- 发起审批失败。
- 审批无法处理。
- 工作流消息完全不生成。
- 抄送已读状态不可信。
