# 多端会话管理任务执行文档

## 任务清单

- [x] 后端测试：新增同账号多会话和单端强制下线集成测试。
- [x] 契约调整：在线用户 DTO 增加 `sessionId`、设备和浏览器字段。
- [x] token 调整：登录时生成 `sessionId` 并写入 JWT。
- [x] 会话记录：登录成功时新增会话记录，认证请求按 `sessionId` 刷新活跃时间。
- [x] 会话去重：同账号、同 IP、同 User-Agent 新登录时自动下线旧会话。
- [x] 会话校验：JWT 校验时确认 `sessionId` 仍在线。
- [x] 强制下线：新增按 session 下线接口，保留按 userId 下线所有会话。
- [x] EF 映射：`OnlineUser` 改为 `SessionId` 主键，保留 `UserId` 索引。
- [x] MySQL 兼容：初始化时补齐 `SessionId`、`DeviceName`、`BrowserName` 字段和索引。
- [x] 前端 API：在线用户类型和单端下线接口改造。
- [x] 前端页面：在线用户列表改为会话列表。
- [x] 前端状态：登录成功保存当前 `sessionId`，踢会话时按 `sessionId` 判断是否为当前会话。
- [x] 总结文档：记录实现、数据流和验证结果。
- [x] 验证：运行后端测试、前端构建并启动前后端。

## 文件计划

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 增加会话级测试 |
| `src/MiniAdmin.Domain/Entities/OnlineUser.cs` | 修改 | 增加会话字段 |
| `src/MiniAdmin.Application.Contracts/Auth/ITokenService.cs` | 修改 | token 创建传入 sessionId |
| `src/MiniAdmin.Infrastructure/Auth/JwtTokenService.cs` | 修改 | JWT 增加 `session_id` |
| `src/MiniAdmin.Application.Contracts/OnlineUsers/*` | 修改 | DTO 和接口增加 sessionId |
| `src/MiniAdmin.Application/OnlineUsers/OnlineUserAppService.cs` | 修改 | 透传会话能力 |
| `src/MiniAdmin.Infrastructure/Persistence/EfOnlineUserRepository.cs` | 修改 | 多会话记录、校验、下线 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs` | 修改 | EF 映射 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | MySQL 表兼容升级 |
| `src/MiniAdmin.Api/Program.cs` | 修改 | JWT 校验、登录记录、单端下线端点 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/system/online-user.ts` | 修改 | 类型和接口 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/system/online-user/index.vue` | 修改 | 会话列表展示 |

## 实施顺序

1. 写失败测试，确认当前只能按用户聚合且不能单端下线。
2. 改契约和 token，编译到能表达 sessionId。
3. 改仓储和 EF 映射，让测试过绿。
4. 接前端页面。
5. 补总结文档。
6. 运行完整验证并启动服务。
