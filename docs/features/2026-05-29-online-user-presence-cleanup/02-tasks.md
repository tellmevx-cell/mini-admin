# 在线用户活跃状态清理任务执行文档

## 任务清单

- [x] 复现在线用户旧记录仍显示的问题。
- [x] 增加回归测试覆盖过期在线记录。
- [x] 增加在线用户活跃配置。
- [x] 在线用户列表查询前清理过期记录。
- [x] JWT 校验通过后刷新用户最近活跃时间。
- [x] 系统监控在线人数使用活跃时间口径。
- [x] 执行完整后端测试。
- [x] 启动后端和前端。

## 涉及文件

- `src/MiniAdmin.Api/Program.cs`
- `src/MiniAdmin.Api/appsettings.json`
- `src/MiniAdmin.Application.Contracts/OnlineUsers/IOnlineUserAppService.cs`
- `src/MiniAdmin.Application.Contracts/OnlineUsers/IOnlineUserRepository.cs`
- `src/MiniAdmin.Application/OnlineUsers/OnlineUserAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfOnlineUserRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/OnlineUserOptions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Infrastructure/SystemMonitor/SystemMonitorAppService.cs`
- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 先写测试，构造 `IsOnline=true` 但 `LastActiveAt` 很早的记录。
2. 运行测试，确认旧实现会把过期记录返回。
3. 增加 `OnlineUserOptions`，提供活跃超时和刷新节流配置。
4. 在线用户查询前将过期记录置为离线，并只返回仍活跃的记录。
5. 在 JWT 校验通过后调用在线用户活跃刷新逻辑。
6. 系统监控看板按相同活跃窗口统计在线人数。
7. 运行测试和启动服务验证。

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "FullyQualifiedName~OnlineUserList_Does_Not_Return_Stale_Online_Records"
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

## 当前状态

已完成，后端和前端均已启动供页面验证。
