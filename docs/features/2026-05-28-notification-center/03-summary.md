# 通知中心总结文档

## 完成内容

- `GET /notification/my` 支持分页和筛选参数：
  - `page`
  - `pageSize`
  - `take`
  - `isRead`
  - `category`
  - `sourceType`
- 通知列表的 `Total` 按筛选条件统计，`UnreadCount` 始终统计当前用户所有未读通知，方便顶部铃铛使用。
- `系统监控` 下新增 `通知中心` 菜单，路径为 `/system/notification`。
- 新增权限编码 `system:notification:query`，admin 默认拥有该菜单入口。
- Vben 新增通知中心页面，支持查询、重置、分页、单条已读、全部已读、单条删除、清空通知、跳转通知链接。
- 顶部通知弹层的“查看全部”已改为跳转通知中心。

## 关键文件

- 后端契约：`src/MiniAdmin.Application.Contracts/UserNotifications/UserNotificationDtos.cs`
- 后端仓储：`src/MiniAdmin.Infrastructure/Persistence/EfUserNotificationRepository.cs`
- API 入口：`src/MiniAdmin.Api/Program.cs`
- 菜单种子：`src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- 前端 API：`frontend/vue-vben-admin/apps/web-antd/src/api/core/notification.ts`
- 前端页面：`frontend/vue-vben-admin/apps/web-antd/src/views/system/notification/index.vue`
- 顶部通知：`frontend/vue-vben-admin/apps/web-antd/src/layouts/basic.vue`

## 验证结果

- `dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "Notification|MenuAll..." ...`：通过 5 个测试。
- `dotnet build MiniAdmin.slnx`：通过，0 警告，0 错误。
- `dotnet test MiniAdmin.slnx ...`：通过 84 个测试。
- `pnpm run build:antd`：通过，Vben web-antd 构建完成。

## 后续建议

- 下一步可以做通知生成规则配置，例如哪些告警等级通知哪些角色。
- 如果后续要做实时提醒，可以在当前通知中心基础上接 SignalR，不需要重做页面结构。
