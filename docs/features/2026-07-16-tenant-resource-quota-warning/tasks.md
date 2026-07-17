# 租户资源用量看板与配额预警任务

## 实现清单

- [x] 分析现有配额、通知、定时任务和工作台结构。
- [x] 明确阈值、统计口径、通知幂等和恢复规则。
- [x] 新增预警状态实体、DbContext 配置和数据库迁移。
- [x] 实现当前租户用量查询与全租户预警扫描服务。
- [x] 新增动态 API 和依赖注入注册。
- [x] 接入每日定时任务、默认模板、消息中心和 SignalR。
- [x] 增强租户工作台资源看板。
- [x] 增强平台租户列表状态展示。
- [x] 补齐边界、幂等、恢复后再次告警和接口测试。
- [x] 执行全量测试、前端构建和文档构建。

## 验证结果

- 配额预警专项测试：`1/1` 通过。
- 后端全量测试：`269/269` 通过。
- Ant Design Vue 前端生产构建：通过。
- VitePress 文档构建：通过。

## 实现顺序

1. 建立状态实体和增量迁移，确保新旧数据库均可升级。
2. 实现统一的资源指标计算和状态机。
3. 实现扫描、状态持久化、租户管理员解析和通知幂等。
4. 注册定时任务与默认消息模板。
5. 暴露动态 API，完成工作台和平台列表展示。
6. 通过专项测试与全量构建验证后更新文档状态。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter TenantResourceQuotaWarning
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
pnpm --dir C:\monica\code\mini-admin\frontend\vue-vben-admin run build:antd
pnpm --dir C:\monica\code\mini-admin\docs-site run build
```
