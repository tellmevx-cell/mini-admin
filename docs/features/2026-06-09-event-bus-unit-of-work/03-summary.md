# 事件总线与工作单元总结

## 完成内容

- 新增本地事件抽象：`ILocalEvent`、`ILocalEventBus`、`ILocalEventHandler<TEvent>`。
- 新增本地事件总线实现：`LocalEventBus`。
- 新增工作单元抽象：`IUnitOfWork`。
- 新增 EF Core 工作单元实现：`EfUnitOfWork`。
- 在 `AddMiniAdminPersistence` 中注册事件总线和工作单元。
- 支持扫描注册 Infrastructure 程序集内的本地事件 handler。
- 支持提交后事件派发和回滚清理。
- 保留现有 Repository 的 `SaveChangesAsync` 行为，不强制迁移。

## 关键实现

`LocalEventBus` 通过 DI 查找 `ILocalEventHandler<TEvent>` 并顺序执行。泛型发布和运行时事件发布都支持。

`EfUnitOfWork` 基于 `MiniAdminDbContext` 提供事务控制：

- `BeginTransactionAsync`
- `SaveChangesAsync`
- `CommitAsync`
- `RollbackAsync`
- `AddPostCommitEvent`

如果底层数据库是 EF InMemory，事务开始会降级为 no-op 事务，以保证测试和本地演示不被事务支持问题阻断。

## 使用方式

定义事件：

```csharp
public sealed record UserRoleChangedEvent(Guid UserId) : ILocalEvent;
```

定义 handler：

```csharp
public sealed class UserRoleChangedEventHandler : ILocalEventHandler<UserRoleChangedEvent>
{
    public Task HandleAsync(UserRoleChangedEvent @event, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
```

使用工作单元：

```csharp
await unitOfWork.BeginTransactionAsync(cancellationToken);
unitOfWork.AddPostCommitEvent(new UserRoleChangedEvent(userId));
await unitOfWork.CommitAsync(cancellationToken);
```

## 影响范围

这是新增平台底座能力，不改变现有接口和页面行为。

现有简单 CRUD 和已有工作流/消息中心代码可以继续按原方式运行。后续复杂业务再逐步接入 `IUnitOfWork` 和本地事件总线。

## 后续建议

- 有明确跨系统或跨进程需求时，再扩展 Outbox 和分布式事件。
- 新增业务模块如果涉及提交后通知、清缓存或状态同步，优先使用本地事件。
- 不建议把所有简单 CRUD 都强制包进显式事务，避免过度工程化。
