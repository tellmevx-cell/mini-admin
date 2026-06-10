# 审计日志基础能力任务执行文档

> 回补整理。

## 任务清单

- [x] 定义审计日志实体
- [x] 新增审计日志保存请求和查询 DTO
- [x] 实现审计日志中间件
- [x] 捕获请求体
- [x] 实现日志列表查询
- [x] 实现日志导出
- [x] 前端审计日志页面
- [x] 接入权限码

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/AuditLog.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/AuditLogDto.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/AuditLogListQuery.cs`
- `src/MiniAdmin.Application.Contracts/AuditLogs/SaveAuditLogRequest.cs`
- `src/MiniAdmin.Application/AuditLogs/AuditLogAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAuditLogRepository.cs`
- `src/MiniAdmin.Api/AuditLogMiddleware.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/audit-log.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/log/index.vue`

## 执行步骤

1. 创建 `AuditLog` 实体。
2. 新增日志保存、查询 DTO。
3. 编写 `AuditLogMiddleware` 捕获请求上下文。
4. 在 `Program.cs` 注册中间件。
5. 实现日志列表和导出接口。
6. 前端增加审计日志页面。
7. 为查询和导出增加权限码。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminAuditDocs'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Audit|Log"
```

## 当前状态

已完成。

