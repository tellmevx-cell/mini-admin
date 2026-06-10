# 在线用户活跃状态清理完工总结

## 完成内容

- 在线用户列表增加活跃超时口径，旧记录不会继续显示为在线。
- 查询在线用户前会自动把过期在线记录标记为离线。
- JWT 校验通过后会刷新当前用户最近活跃时间，并通过节流避免每个请求都写库。
- 系统监控看板的在线人数同步改为按活跃时间统计。

## 关键实现

- 新增 `OnlineUserOptions`，默认 `ActiveTimeoutMinutes=30`、`TouchThrottleSeconds=60`。
- `EfOnlineUserRepository.GetOnlineUsersAsync` 查询前执行过期清理，并按 `LastActiveAt` 过滤。
- `EfOnlineUserRepository.TouchAsync` 支持刷新在线用户活跃状态。
- `Program.cs` 在 JWT 安全戳校验通过后调用在线用户活跃刷新。
- `SystemMonitorAppService` 使用同一活跃窗口统计在线人数。

## 影响范围

- 后端在线用户查询接口：`/system/online-user/list`
- 后端系统监控接口：`/system/monitor/overview`
- JWT 鉴权成功后的在线状态刷新逻辑
- 默认配置文件：`src/MiniAdmin.Api/appsettings.json`

## 验证结果

- 已确认新增回归测试在旧实现下失败。
- 已确认新增回归测试在修复后通过。
- 已执行完整后端测试：`92/92` 通过。
- 已启动后端：`http://localhost:5320/health` 返回 `Healthy`。
- 已确认前端：`http://localhost:5666/` 返回 `200 OK`。

## 使用方式

- 默认 30 分钟未活跃的用户不再算在线。
- 如需调整，可在配置文件中修改：

```json
{
  "OnlineUsers": {
    "ActiveTimeoutMinutes": 30,
    "TouchThrottleSeconds": 60
  }
}
```

## 后续建议

- 后续如果要支持多端登录明细，可以把当前按用户聚合的在线表升级为按会话聚合。
- 可以增加批量强制下线和按 IP、浏览器筛选。
