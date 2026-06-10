# 安全中心任务执行文档

## 任务清单

- [x] 创建需求文档。
- [x] 用户确认第一阶段范围。
- [x] 梳理现有用户、角色、菜单、登录、在线用户、审计日志代码。
- [x] 设计安全事件实体、DTO、仓储和服务接口。
- [x] 增加安全事件表、EF Migration 和 MySQL 兼容初始化。
- [x] 在登录失败、账号锁定、强制下线、用户禁用、权限变更等节点写入事件。
- [x] 实现安全中心概览接口。
- [x] 实现安全事件列表接口。
- [x] 实现最后一个管理员保护。
- [x] 实现用户禁用后强制失效 token 并移出在线用户。
- [x] 补充菜单、权限和种子数据。
- [x] 实现 Vben 安全中心页面。
- [x] 编写和运行后端测试。
- [x] 运行前端构建。
- [x] 启动后端和前端供验证。
- [x] 编写完工总结。

## 涉及文件

预计涉及：

- `src/MiniAdmin.Domain/Entities/SecurityEvent.cs`
- `src/MiniAdmin.Application.Contracts/Security/*`
- `src/MiniAdmin.Application/Security/*`
- `src/MiniAdmin.Infrastructure/Persistence/EfSecurityEventRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/Migrations/20260529014045_AddSecurityEvents.cs`
- `src/MiniAdmin.Api/Program.cs`
- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/security-center/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/security-center.ts`

## 执行步骤

1. 先确认安全中心第一阶段边界。
2. 写后端测试覆盖安全事件写入、看板聚合、最后管理员保护。
3. 增加领域实体和 EF 映射。
4. 实现仓储、应用服务和 API。
5. 接入已有业务节点，记录安全事件。
6. 增加菜单和权限种子。
7. 实现前端页面。
8. 完整验证并启动服务。

## 测试命令

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
pnpm run build:antd
```

## 当前状态

实现、测试和服务启动已完成。
