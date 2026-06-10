# 工作流与消息中心冒烟测试手册

## 目标

这份手册只用于上线前或联调前快速确认工作流、消息中心、抄送已读和通知投递链路可用，不继续增加催读、加签、代理审批、统计大屏等扩展功能。

## 本地启动

### 1. 启动后端

在仓库根目录执行：

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

启动后检查：

```powershell
Invoke-WebRequest -Uri http://localhost:5021/health -UseBasicParsing
```

默认配置使用 `InMemory` 数据库，启动时会自动初始化菜单、权限、用户、工作流示例和通知策略。

### 2. 启动前端

在前端目录执行：

```powershell
cd frontend/vue-vben-admin
pnpm run dev:antd
```

打开：

```text
http://localhost:5666
```

默认登录账号：

```text
admin / 123456
```

### 3. 默认通知配置

`src/MiniAdmin.Api/appsettings.json` 中邮件和 Webhook 默认关闭：

```json
{
  "Notifications": {
    "Email": {
      "Enabled": false
    },
    "Webhook": {
      "Enabled": false
    }
  }
}
```

默认状态下，工作流通知会走站内信；如果通知策略打开了邮件或 Webhook，但配置仍为空，会生成 `Skipped` 或 `Failed` 投递记录，并向管理员发送投递失败站内告警。

## 冒烟测试清单

### 1. 菜单与基础入口

- 使用 `admin / 123456` 登录。
- 确认左侧菜单能看到 `工作流 / 审批中心`。
- 确认顶部铃铛入口可打开消息列表。
- 确认 `系统管理 / 消息通知中心` 可打开。

通过标准：

- 页面不报错。
- 审批中心能看到 `我的待办`、`我的申请`、`我的抄送`、`发起审批`、`流程实例`、`流程定义`、`业务绑定`、`我的已办`。
- 消息通知中心能看到概览、我的消息、投递记录、通知策略、订阅偏好等工作区。

### 2. 创建或确认请假审批流程

进入 `工作流 / 审批中心 / 流程定义`：

- 如果已有 `请假审批示例`，确认它已启用。
- 如果没有示例，点击示例相关按钮生成请假流程。
- 保存草稿后发布流程。

通过标准：

- 流程定义列表存在启用状态的请假流程。
- 流程编码可用于发起审批。

### 3. 发起请假审批

进入 `工作流 / 审批中心 / 发起审批`：

- 选择请假审批流程。
- 填写业务标识，例如 `LEAVE-20260609-001`。
- 填写审批标题，例如 `请假申请`。
- 表单 JSON 使用：

```json
{
  "days": 5,
  "reason": "家中有事，需要请假",
  "type": "事假"
}
```

通过标准：

- 发起成功。
- `我的申请` 出现该流程实例。
- 审批人账号的 `我的待办` 出现审批任务。
- 顶部铃铛或消息中心出现对应工作流通知。

### 4. 审批通过链路

进入 `我的待办`：

- 打开审批详情。
- 点击同意。
- 填写审批意见，例如 `可以的`。

通过标准：

- 当前任务进入 `已通过` 或继续流转到下一审批节点。
- 当前用户 `我的已办` 能看到处理记录。
- 发起人收到审批进展站内信。
- `流程实例` 的流转记录能看到发起、同意等动作。

### 5. 审批驳回链路

重新发起一条请假审批，进入待办详情：

- 点击驳回。
- 填写意见，例如 `请补充请假说明`。

通过标准：

- 流程实例状态变为已驳回。
- 发起人收到驳回通知。
- 流程详情中能看到驳回节点、处理人、处理时间和意见。

### 6. 抄送已读/未读链路

使用包含抄送节点的流程，或让请假天数命中示例流程中的抄送路径：

- 发起审批并流转到抄送节点。
- 切换到被抄送人的账号或使用管理员检查 `我的抄送`。
- 在 `我的抄送` 中先筛选未读。
- 打开详情，关闭后再次筛选未读。

通过标准：

- 抄送记录初始为未读。
- 打开抄送详情后自动标记已读。
- 再次筛选未读时，该记录不再出现在未读列表。
- 流程详情 Drawer 的抄送回执能展示已读/未读和阅读时间。

### 7. 消息跳转与自动已读

触发一条工作流通知后：

- 点击顶部铃铛中的工作流消息。
- 或进入 `系统管理 / 消息通知中心 / 我的消息` 点击详情。

通过标准：

- 工作流消息能跳到对应审批详情。
- 如果消息携带抄送记录参数，打开详情后会同步触发抄送已读。
- 关闭 Drawer 后仍停留在合理的审批中心上下文，不出现反复跳转或重复弹窗。

### 8. 投递失败与重试链路

进入 `系统管理 / 消息通知中心 / 通知策略`：

- 打开某个工作流事件的邮件或 Webhook 通道。
- 保持 `appsettings.json` 中邮件或 Webhook 配置为空。
- 再触发一条对应工作流通知。

通过标准：

- `投递记录` 中出现 `Skipped` 或 `Failed` 记录。
- 管理员收到 `NotificationDeliveryFailure` 站内告警。
- 告警链接能跳转到消息通知中心，并定位到投递记录筛选上下文。
- 修复配置后，点击投递记录的重试按钮，重试次数和最终状态会刷新。

### 9. 订阅偏好恢复默认

进入 `系统管理 / 消息通知中心 / 订阅偏好`：

- 修改某个事件的个人订阅偏好。
- 刷新列表，确认偏好已保存。
- 点击 `全部恢复默认`。

通过标准：

- 自定义偏好被清除。
- 当前用户重新跟随全局通知策略。
- 全局策略关闭的通道，个人偏好不能强行打开。

## 自动化验证命令

后端关键测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "FullyQualifiedName~WorkflowAppServiceTests|FullyQualifiedName~NotificationDeliveryServiceTests|FullyQualifiedName~NotificationPolicyAppServiceTests"
```

前端类型检查：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

前端构建：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd build
```

文档和空白检查：

```powershell
git diff --check
```

## 常见问题

### 1. 前端请求接口 404 或无响应

检查后端是否启动在 `http://localhost:5021`。前端开发代理配置位于：

```text
frontend/vue-vben-admin/apps/web-antd/vite.config.ts
```

当前 `/api` 代理目标是：

```text
http://localhost:5021
```

### 2. 后端构建提示 DLL 被占用

通常是 `MiniAdmin.Api` 仍在运行。先查看并停止进程：

```powershell
Get-Process MiniAdmin.Api -ErrorAction SilentlyContinue
Stop-Process -Id <pid>
```

只停止确认属于当前仓库的本地开发进程，不要误停其他服务。

### 3. 看不到新菜单或按钮

优先处理顺序：

- 退出登录后重新登录。
- 确认当前用户拥有 `admin` 角色。
- InMemory 模式下可重启后端重新初始化种子数据。
- MySQL 模式下确认初始化脚本或种子逻辑已经执行。

### 4. 工作流没有生成通知

优先检查：

- 通知策略是否启用对应事件。
- 当前用户是否在订阅偏好中关闭了该事件或通道。
- 工作流是否实际流转到对应节点。
- 消息是否因为同一 `SourceType + SourceId + UserId` 幂等规则被去重。

### 5. 邮件或 Webhook 没有实际发出

默认配置下不会实际发送外部消息。需要先配置：

- `Notifications:Email:Enabled`
- `Notifications:Email:Host`
- `Notifications:Email:Port`
- `Notifications:Email:UserName`
- `Notifications:Email:Password`
- `Notifications:Email:FromEmail`
- `Notifications:Webhook:Enabled`
- `Notifications:Webhook:EndpointUrl`

配置为空时属于预期跳过或失败，应在投递记录中排查。

## 收口标准

本阶段只要下面几项通过，就可以进入后续业务模块联调：

- 请假审批能发起、同意、驳回。
- 工作流站内信能生成、跳转、已读。
- 抄送记录能区分已读/未读，并能在流程详情查看回执。
- 邮件/Webhook 配置异常时能生成投递记录和管理员告警。
- 个人订阅偏好可以修改并一键恢复默认。

暂不继续开发催读、加签、代理审批、统计大屏等非必要功能。
