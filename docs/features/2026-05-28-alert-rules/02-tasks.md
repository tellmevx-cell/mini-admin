# 告警规则配置任务执行文档

## 任务清单

- [x] 后端契约：新增告警规则 DTO、查询、更新请求和服务接口。
- [x] 数据模型：新增 `AlertRule` 实体、DbContext 映射和迁移。
- [x] 数据初始化：种子 5 条内置告警规则和菜单权限。
- [x] 后端仓储：实现规则列表、单条更新、启用规则读取。
- [x] 告警扫描：改为按启用规则配置生成信号，并尊重通知开关。
- [x] 后端 API：新增查询和更新接口，挂接 RBAC 权限。
- [x] 前端 API：新增 `api/system/alert-rule.ts`。
- [x] 前端页面：新增 `views/system/alert-rule/index.vue`。
- [x] 测试验证：补后端测试并跑前后端构建。
- [x] 总结文档：完成后补 `03-summary.md`。

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/AlertRule.cs`
- `src/MiniAdmin.Application.Contracts/Alerts/AlertRuleDtos.cs`
- `src/MiniAdmin.Application/Alerts/AlertRuleAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAlertRuleRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/alert-rule.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/alert-rule/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-05-28-alert-rules/01-requirements.md`
- `docs/features/2026-05-28-alert-rules/02-tasks.md`
- `docs/features/2026-05-28-alert-rules/03-summary.md`

## 执行步骤

1. 编写后端失败测试，覆盖默认规则、菜单权限、启停规则、通知开关。
2. 新增实体、DbContext 映射和迁移。
3. 新增仓储和应用服务。
4. 调整 `AlertAppService.ScanAsync` 使用规则配置。
5. 新增 Minimal API 接口和权限校验。
6. 种子菜单、权限和默认规则。
7. 新增前端 API 和页面。
8. 跑后端测试。
9. 跑前端构建。
10. 补完工总结并提交代码。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "AlertRule|AlertScan|MenuAll"
```

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 当前状态

已完成实现、验证和总结。
