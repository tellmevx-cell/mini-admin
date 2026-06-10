# 事件总线与工作单元任务

## 任务清单

- [x] 新增本地事件接口。
- [x] 新增本地事件总线实现。
- [x] 新增工作单元接口。
- [x] 新增 EF Core 工作单元实现。
- [x] 注册事件总线和工作单元。
- [x] 支持提交后事件派发。
- [x] 支持回滚清除事件。
- [x] 支持 EF InMemory 无事务环境下的 no-op 事务。
- [x] 补充自动化测试。
- [x] 补充文档站说明。

## 涉及文件

- `src/MiniAdmin.Application.Contracts/Events/LocalEvents.cs`
- `src/MiniAdmin.Application.Contracts/UnitOfWork/IUnitOfWork.cs`
- `src/MiniAdmin.Infrastructure/Events/LocalEventBus.cs`
- `src/MiniAdmin.Infrastructure/UnitOfWork/EfUnitOfWork.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `tests/MiniAdmin.Tests/PlatformInfrastructureTests.cs`
- `docs-site/developer/event-bus-unit-of-work.md`

## 测试命令

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter PlatformInfrastructureTests --no-restore
```

回归测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests|NotificationTemplateAppServiceTests|NotificationPolicyAppServiceTests|NotificationDeliveryServiceTests" --no-restore
```

文档站构建：

```powershell
pnpm docs:build
```

## 当前状态

已完成。
