# 通知通道与接收人配置需求文档

## 背景

告警规则已经支持阈值、启停和通知开关，但接收人仍固定为管理员角色，通知方式也只有站内信。企业级后台需要按告警类型配置接收人，并支持邮件这种系统外触达方式。

## 功能目标

- 支持为每条告警规则配置接收角色。
- 支持为每条告警规则配置指定用户。
- 支持站内信和邮件两个通知通道。
- 用户资料支持邮箱字段。
- 告警触发后按接收人配置推送站内信和邮件。
- 记录邮件发送结果。

## 功能范围

- 新增 `mini_alert_rule_recipients`。
- 新增 `mini_notification_deliveries`。
- `mini_alert_rules` 增加 `EmailEnabled`。
- `mini_users` 增加 `Email`。
- 告警规则列表和编辑接口返回/更新接收人和邮件开关。
- Vben 告警规则编辑弹窗增加通知配置区域。
- 新增 SMTP 邮件发送服务。
- 新增集成测试覆盖接收人解析、站内信、邮件发送记录和失败保护。
- 用户导入导出第一版不扩展邮箱字段，后续单独处理模板兼容。

## 不做范围

- 不做短信。
- 不做 Webhook 实际发送。
- 不做页面维护 SMTP 密码。
- 不做部门和岗位接收人。
- 不做模板编辑器。

## 权限

- 继续使用 `system:alert-rule:query` 查看配置。
- 继续使用 `system:alert-rule:update` 更新配置。
- 邮件发送记录第一版不单独做页面权限；如需要页面，后续再补 `system:notification-delivery:query`。

## 配置要求

SMTP 密码只能通过本地配置或环境变量提供：

```json
"Notifications": {
  "Email": {
    "Enabled": true,
    "Host": "smtp.example.com",
    "Port": 465,
    "UserName": "notice@example.com",
    "Password": "your-app-password",
    "FromEmail": "notice@example.com",
    "FromName": "MiniAdmin",
    "EnableSsl": true
  }
}
```

## 验收标准

- [x] 告警规则可以保存接收角色和指定用户。
- [x] 默认 5 条告警规则都有 `admin` 角色接收人。
- [x] 角色接收人和指定用户会合并去重。
- [x] 告警触发时接收人收到站内信。
- [x] 邮件开启且用户邮箱存在时会产生邮件发送记录。
- [x] SMTP 未配置或发送失败不会导致告警扫描失败。
- [x] 用户新增、编辑、列表支持邮箱。
- [x] 后端测试通过，前端构建通过。
