# 事件总线与工作单元

MiniAdmin 已经内置轻量本地事件总线和 EF Core 工作单元，用于后续二开时处理复杂写操作、提交后通知、缓存刷新和模块解耦。

访问路径：

```text
文档站 -> 二开 -> 事件总线与工作单元
```

本地文档站地址：

```text
http://localhost:5173/developer/event-bus-unit-of-work
```

## 适用场景

当一个业务动作只做一次简单 CRUD 时，继续使用现有 Repository 即可，不需要额外包一层。

当一个业务动作变成下面这种链路时，就应该考虑事件总线和工作单元：

```text
保存主数据
  -> 写明细
  -> 更新审批状态
  -> 提交事务
  -> 发站内信
  -> 清权限缓存
  -> 写扩展日志
```

核心目标是两件事：

- **一致性**：数据库提交成功后，再执行通知、清缓存等副作用。
- **解耦**：主流程不直接依赖消息、缓存、日志等扩展服务。

## 能力组成

| 能力 | 接口/实现 | 说明 |
| --- | --- | --- |
| 本地事件 | `ILocalEvent` | 事件标记接口 |
| 事件总线 | `ILocalEventBus` / `LocalEventBus` | 发布应用内事件 |
| 事件处理器 | `ILocalEventHandler<TEvent>` | 处理某一种事件 |
| 工作单元 | `IUnitOfWork` / `EfUnitOfWork` | 控制事务、保存变更和提交后事件 |

## 源码位置

| 文件 | 说明 |
| --- | --- |
| `src/MiniAdmin.Application.Contracts/Events/LocalEvents.cs` | 事件总线接口 |
| `src/MiniAdmin.Application.Contracts/UnitOfWork/IUnitOfWork.cs` | 工作单元接口 |
| `src/MiniAdmin.Infrastructure/Events/LocalEventBus.cs` | 本地事件总线实现 |
| `src/MiniAdmin.Infrastructure/UnitOfWork/EfUnitOfWork.cs` | EF Core 工作单元实现 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs` | DI 注册入口 |
| `tests/MiniAdmin.Tests/PlatformInfrastructureTests.cs` | 行为测试 |

## 执行流程

```text
应用服务
  |
  | BeginTransactionAsync
  v
工作单元开启事务
  |
  | 执行业务写入
  | AddPostCommitEvent
  v
CommitAsync
  |
  | 数据库提交成功
  v
LocalEventBus 发布事件
  |
  v
一个或多个 ILocalEventHandler<TEvent> 顺序处理
```

如果执行 `RollbackAsync`，工作单元会清空提交后事件，不会发布事件。

## 定义事件

事件建议使用 `record`，只包含处理器需要的最小信息。

```csharp
using MiniAdmin.Application.Contracts.Events;

public sealed record UserRoleChangedEvent(
    Guid UserId,
    string UserName) : ILocalEvent;
```

命名建议：

- 使用过去式或事实描述，例如 `UserRoleChangedEvent`、`WorkflowApprovedEvent`。
- 不要使用命令式名称，例如 `SendNotificationEvent`。
- 事件里不要塞完整实体，优先放 ID 和必要快照字段。

## 定义事件处理器

事件处理器实现 `ILocalEventHandler<TEvent>`。

```csharp
using MiniAdmin.Application.Contracts.Events;
using MiniAdmin.Application.Contracts.Caching;

public sealed class ClearUserAuthorizationCacheHandler(
    IUserAuthorizationCache authorizationCache)
    : ILocalEventHandler<UserRoleChangedEvent>
{
    public async Task HandleAsync(
        UserRoleChangedEvent @event,
        CancellationToken cancellationToken = default)
    {
        await authorizationCache.RemoveUserAsync(@event.UserId, cancellationToken);
    }
}
```

处理器适合做：

- 清权限缓存。
- 发站内信。
- 写扩展日志。
- 同步非核心状态。
- 触发后续轻量平台动作。

处理器不适合做：

- 长时间阻塞操作。
- 不可控外部系统调用。
- 需要强一致的核心写入。
- 复杂业务主流程。

## 直接发布事件

如果不需要事务提交后再发布，可以直接注入 `ILocalEventBus`：

```csharp
public sealed class DemoAppService(ILocalEventBus localEventBus)
{
    public async Task RunAsync(Guid userId, CancellationToken cancellationToken)
    {
        await localEventBus.PublishAsync(
            new UserRoleChangedEvent(userId, "admin"),
            cancellationToken);
    }
}
```

这种方式适合纯内存动作、测试工具或不依赖数据库提交结果的事件。

## 使用工作单元

典型写法：

```csharp
public sealed class UserRoleAssignmentService(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork)
{
    public async Task AssignRoleAsync(
        Guid userId,
        Guid roleId,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            await userRepository.AssignRoleAsync(userId, roleId, cancellationToken);

            unitOfWork.AddPostCommitEvent(
                new UserRoleChangedEvent(userId, "admin"));

            await unitOfWork.CommitAsync(cancellationToken);
        }
        catch
        {
            await unitOfWork.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
```

注意：

- `AddPostCommitEvent` 只是登记事件，不会立即执行。
- `CommitAsync` 成功后才会派发事件。
- `RollbackAsync` 会清空事件。
- handler 抛出的原始异常会向上抛出，便于调用方捕获和排查。

## 无显式事务的写法

如果只是想复用 `SaveChangesAsync` 并在保存成功后派发事件，可以这样写：

```csharp
unitOfWork.AddPostCommitEvent(new UserRoleChangedEvent(userId, "admin"));
await unitOfWork.SaveChangesAsync(cancellationToken);
```

派发规则：

| 场景 | 事件什么时候派发 |
| --- | --- |
| 没有显式事务，调用 `SaveChangesAsync` | 保存成功后立即派发 |
| 显式事务中，调用 `SaveChangesAsync` | 暂不派发 |
| 显式事务中，调用 `CommitAsync` | 提交成功后派发 |
| 显式事务中，调用 `RollbackAsync` | 不派发 |

## 和现有 Repository 的关系

当前实现是低侵入接入：

- 不强制替换现有 Repository。
- 不改变现有 `DbContext.SaveChangesAsync` 行为。
- 不要求已有工作流、消息中心、用户管理立刻迁移。
- 后续新增复杂业务时再主动使用 `IUnitOfWork`。

也就是说，现有模块可以继续稳定运行；这套能力主要给后续复杂用例和二开模块使用。

## 推荐使用边界

建议使用：

- 一个用例需要多个写操作保持一致。
- 提交成功后要发消息。
- 提交成功后要清缓存。
- 主流程不想直接依赖副作用服务。
- 后续可能新增多个事件处理器。

不建议使用：

- 简单单表 CRUD。
- 只读查询。
- 没有事务一致性要求的轻量动作。
- 为了“看起来架构完整”而过度包装。

## 测试覆盖

当前测试文件：

```text
tests/MiniAdmin.Tests/PlatformInfrastructureTests.cs
```

已覆盖：

- 事件按 handler 注册顺序发布。
- handler 异常向上抛出。
- 无显式事务时，保存成功后派发事件。
- 显式事务中，提交后派发事件。
- 回滚后不派发事件。
- 提交后事件 handler 异常保留原始异常。

运行测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter PlatformInfrastructureTests --no-restore
```

## 后续升级方向

当前是单体应用内事件，不是分布式消息。

如果未来系统拆分为多个服务，可以在当前抽象基础上升级：

- Outbox 表。
- 分布式事件。
- 消息重试。
- 事件幂等。
- 跨服务链路追踪。

现阶段不建议提前引入 RabbitMQ、Kafka 或复杂 Outbox，除非已经有明确跨服务事件需求。
