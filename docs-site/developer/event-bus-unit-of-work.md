# 事件总线与工作单元

MiniAdmin 提供三种事件方式：进程内立即事件、事务提交后事件、事务 Outbox 可靠事件。二开时应按一致性要求选择，不要把所有动作都包装成事件。

## 能力与选择

| 方式 | API | 持久化 | 失败恢复 | 适用场景 |
| --- | --- | --- | --- | --- |
| 立即事件 | `ILocalEventBus.PublishAsync` | 否 | 否 | 无数据库事务的轻量进程内解耦 |
| 提交后事件 | `IUnitOfWork.AddPostCommitEvent` | 否 | 否 | 提交后清本地缓存等可重建动作 |
| 可靠事件 | `IUnitOfWork.AddOutboxEvent` | 是 | 自动重试、死信 | 不能因进程重启丢失的后续处理 |

可靠事件并不等于绝对 exactly-once。MiniAdmin 提供“至少一次投递 + Inbox 数据库幂等”；外部 HTTP、邮件或第三方系统仍需使用 `EventId` 做幂等。

## 源码位置

| 文件 | 说明 |
| --- | --- |
| `src/MiniAdmin.Application.Contracts/Events/LocalEvents.cs` | 本地事件与可靠事件标记 |
| `src/MiniAdmin.Application.Contracts/Events/OutboxContracts.cs` | Outbox 查询、租约和运行配置契约 |
| `src/MiniAdmin.Application.Contracts/UnitOfWork/IUnitOfWork.cs` | 工作单元接口 |
| `src/MiniAdmin.Infrastructure/Events` | 序列化、分发、重试和后台 worker |
| `src/MiniAdmin.Infrastructure/UnitOfWork/EfUnitOfWork.cs` | EF Core 事务与 Outbox 同事务写入 |
| `tests/MiniAdmin.Tests/ProductionReliabilityTests.cs` | 租约、重试、死信和 Inbox 测试 |

## 本地提交后事件

本地事件适合“丢失后可以重建”的动作，例如单实例开发环境中的轻量缓存刷新。

```csharp
public sealed record UserRoleChangedEvent(Guid UserId) : ILocalEvent;

await unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    await userRepository.AssignRoleAsync(userId, roleId, cancellationToken);
    unitOfWork.AddPostCommitEvent(new UserRoleChangedEvent(userId));
    await unitOfWork.CommitAsync(cancellationToken);
}
catch
{
    await unitOfWork.RollbackAsync(cancellationToken);
    throw;
}
```

数据库提交后进程如果立刻崩溃，本地提交后事件可能来不及执行。因此它不能用于订单推进、关键通知或其他不可丢失动作。

## 定义可靠事件

可靠事件实现 `IOutboxEvent`，必须提供全局唯一且稳定的 `EventId` 与业务事实发生时间。

```csharp
public sealed record UserRoleChangedReliableEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid UserId,
    Guid RoleId) : IOutboxEvent;
```

约束：

- 事件类型必须是可被 `System.Text.Json` 序列化的公开类型。
- 同一个业务事实重试时必须复用同一个 `EventId`，不能每次生成新 ID。
- 类型全名和程序集名会持久化；改名或移动类型前必须先处理存量 Outbox。
- 新版本字段应尽量向后兼容，避免旧消息无法反序列化。
- 事件只保存 ID 和必要快照，不保存 EF 实体。

## 同事务写入 Outbox

```csharp
await unitOfWork.BeginTransactionAsync(cancellationToken);
try
{
    await userRepository.AssignRoleAsync(userId, roleId, cancellationToken);

    unitOfWork.AddOutboxEvent(new UserRoleChangedReliableEvent(
        Guid.NewGuid(),
        DateTimeOffset.UtcNow,
        userId,
        roleId));

    await unitOfWork.CommitAsync(cancellationToken);
}
catch
{
    await unitOfWork.RollbackAsync(cancellationToken);
    throw;
}
```

业务数据与 `mini_outbox_messages` 在同一个数据库事务中提交：要么同时成功，要么同时回滚。无显式事务时，也可以登记事件后调用 `unitOfWork.SaveChangesAsync`。

## 编写处理器

可靠事件复用 `ILocalEventHandler<TEvent>`：

```csharp
public sealed class UserRoleChangedHandler(IUserAuthorizationCache cache)
    : ILocalEventHandler<UserRoleChangedReliableEvent>
{
    public Task HandleAsync(
        UserRoleChangedReliableEvent @event,
        CancellationToken cancellationToken = default)
    {
        return cache.RemoveUserAsync(@event.UserId, cancellationToken);
    }
}
```

worker 会自动恢复事件创建时的租户上下文。每个处理器的数据库修改和 Inbox 回执位于同一事务中，处理器不要再自行开启独立 EF 事务。

处理器要求：

- 可以安全重试，外部调用携带 `EventId` 作为幂等键。
- 尊重 `CancellationToken`，不要无限阻塞停机。
- 不吞异常；抛出异常后 worker 才能退避重试。
- 不把内存状态当作唯一结果。
- 处理器类改名会被视为新的消费者，改名前应评估存量 Inbox。

## 投递与恢复

```text
业务事务 -> Outbox(Pending) -> 数据库租约 -> Handler
                                    | 成功
                                    v
                              Inbox + Succeeded
                                    |
                                    | 失败
                                    v
                         Retry -> DeadLetter
```

- 多 API 实例通过数据库条件更新抢占消息，不依赖进程内锁。
- 执行期间定期续租；实例失联后，其他实例可在租约过期后接管。
- 默认最多尝试 8 次，按指数退避，最长等待 15 分钟。
- 一个消息有多个处理器时，已成功处理的消费者会被 Inbox 跳过。
- 停机发生在处理完成和状态更新之间时，下一个实例会根据 Inbox 安全收尾。

配置位于 `Outbox`：

```json
{
  "LeaseSeconds": 120,
  "HeartbeatSeconds": 30,
  "PollIntervalSeconds": 2,
  "BatchSize": 20,
  "MaxAttempts": 8,
  "RetryBaseSeconds": 5,
  "RetryMaxSeconds": 900
}
```

## 运维接口

接口不新增业务菜单，复用定时任务权限：

| 接口 | 权限 | 说明 |
| --- | --- | --- |
| `GET /system/outbox-message/list` | `system:scheduled-job:query` | 按状态和事件类型查询 |
| `POST /system/outbox-message/{id}/retry` | `system:scheduled-job:run` | 重投 Retry 或 DeadLetter 消息 |

常见状态：`Pending`、`Processing`、`Retry`、`Succeeded`、`DeadLetter`。死信应先修复处理器或配置，再手工重投，不能只做无限重试。

## 使用边界

建议使用可靠事件：

- 核心事务提交后必须继续推进的内部动作。
- 进程重启后不能永久丢失的通知或同步任务。
- 多实例部署下需要可恢复重试的异步处理。

不建议使用：

- 简单单表 CRUD 或只读查询。
- 必须在当前 HTTP 请求中同步返回结果的逻辑。
- 用 Outbox 替代清晰的业务事务边界。
- 当前没有跨服务需求时提前引入 Kafka 或 RabbitMQ。

生产运行与故障处置见[生产可靠性运行手册](../runbooks/production-reliability.md)。
