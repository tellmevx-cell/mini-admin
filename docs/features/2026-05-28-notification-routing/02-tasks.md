# 通知通道与接收人配置任务执行文档

## 任务清单

- [x] 后端契约：扩展告警规则 DTO 和更新请求，新增接收人 DTO。
- [x] 数据模型：新增 `AlertRuleRecipient`、`NotificationDelivery`，扩展 `AlertRule.EmailEnabled` 和 `User.Email`。
- [x] 数据初始化：5 条内置规则默认接收 `admin` 角色。
- [x] 接收人解析：按角色和指定用户展开，并按用户 ID 去重。
- [x] 站内信推送：从固定 `admin` 角色改为按规则接收人推送。
- [x] 邮件通道：新增 SMTP 配置、邮件发送服务和发送记录。
- [x] 后端 API：告警规则查询和更新支持接收人、邮件开关。
- [x] 前端页面：告警规则编辑弹窗增加通知配置区域。
- [x] 用户邮箱：用户列表、新增、编辑支持邮箱字段，导入导出暂不改。
- [x] 测试验证：补集成测试和前端构建。
- [x] 总结文档：完成后补 `03-summary.md`。

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/AlertRule.cs`
- `src/MiniAdmin.Domain/Entities/AlertRuleRecipient.cs`
- `src/MiniAdmin.Domain/Entities/NotificationDelivery.cs`
- `src/MiniAdmin.Domain/Entities/User.cs`
- `src/MiniAdmin.Application.Contracts/Alerts/AlertRuleDtos.cs`
- `src/MiniAdmin.Application.Contracts/Users/*.cs`
- `src/MiniAdmin.Application/Alerts/AlertAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAlertRuleRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserNotificationRepository.cs`
- `src/MiniAdmin.Infrastructure/Notifications/*`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/alert-rule.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/alert-rule/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-05-28-notification-routing/01-requirements.md`
- `docs/features/2026-05-28-notification-routing/02-tasks.md`
- `docs/features/2026-05-28-notification-routing/03-summary.md`

## 执行步骤

1. 编写失败测试：默认规则接收人、接收人去重、指定用户站内信、邮件发送记录、SMTP 失败不影响扫描。
2. 扩展用户邮箱字段和用户接口。
3. 新增接收人、发送记录实体和 EF 映射。
4. 扩展告警规则仓储，支持读取和更新接收人。
5. 调整告警扫描通知创建逻辑，改为按规则接收人创建站内信。
6. 新增邮件配置、发送接口和 SMTP 实现。
7. 告警扫描触发邮件发送并记录结果。
8. 前端告警规则页面增加接收人和邮件开关。
9. 用户管理页面增加邮箱字段。
10. 跑后端测试、迁移校验和前端构建。
11. 补总结文档并提交。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "NotificationRouting|AlertRule|AlertScanJob|UserEmail"
```

```powershell
dotnet ef migrations has-pending-model-changes --project C:\monica\code\mini-admin\src\MiniAdmin.Infrastructure\MiniAdmin.Infrastructure.csproj --startup-project C:\monica\code\mini-admin\src\MiniAdmin.Api\MiniAdmin.Api.csproj --context MiniAdminDbContext
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成实现、迁移、测试验证和总结文档。
