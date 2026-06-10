# 安全策略配置任务执行文档

## 任务清单

- [x] 后端契约：新增安全策略 DTO、更新请求、服务接口。
- [x] 后端仓储：系统参数仓储增加按 key upsert 能力。
- [x] 后端服务：新增安全策略服务，完成读取、校验、更新。
- [x] 运行时接入：登录安全、在线用户、安全中心概览使用生效策略。
- [x] API 接口：新增 `GET /system/security-policy` 和 `PUT /system/security-policy`。
- [x] 数据初始化：补齐安全策略参数和更新权限种子。
- [x] 后端测试：覆盖读取默认值、更新持久化、登录策略生效。
- [x] 前端 API：新增安全策略请求方法和类型。
- [x] 前端页面：安全中心增加策略配置区。
- [x] 验证：运行后端测试、前端构建，并启动前后端。

## 文件计划

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Application.Contracts/Security/SecurityDtos.cs` | 修改 | 增加安全策略 DTO、请求、接口 |
| `src/MiniAdmin.Application/Security/SecurityPolicyAppService.cs` | 新增 | 策略读取、校验、保存 |
| `src/MiniAdmin.Application.Contracts/Parameters/ISystemParameterRepository.cs` | 修改 | 增加 `UpsertValueByKeyAsync` |
| `src/MiniAdmin.Infrastructure/Persistence/EfSystemParameterRepository.cs` | 修改 | 实现 upsert |
| `src/MiniAdmin.Infrastructure/Auth/DistributedLoginSecurityService.cs` | 修改 | 从策略服务读取登录策略 |
| `src/MiniAdmin.Infrastructure/Persistence/EfOnlineUserRepository.cs` | 修改 | 在线超时/心跳间隔读取策略 |
| `src/MiniAdmin.Infrastructure/Persistence/EfSecurityEventRepository.cs` | 修改 | 安全中心概览读取长期未登录/在线策略 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | 初始化参数和权限 |
| `src/MiniAdmin.Api/Program.cs` | 修改 | 注册服务并新增接口 |
| `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs` | 修改 | 增加集成测试 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/system/security-center.ts` | 修改 | 增加策略接口 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/system/security-center/index.vue` | 修改 | 增加策略配置 UI |

## 实施顺序

1. 先写后端集成测试，确认接口目前不存在或行为不满足。
2. 增加契约、服务、仓储能力，让测试通过。
3. 接入运行时策略读取，补充登录策略生效测试。
4. 增加前端 API 和 UI。
5. 补充总结文档。
6. 执行完整验证。
