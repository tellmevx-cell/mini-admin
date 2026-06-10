# 事件总线与工作单元需求

## 背景与目标

MiniAdmin 已经具备工作流、消息中心、审计、租户、代码生成器等平台能力。随着后续二开模块增多，如果业务动作直接调用通知、缓存、流程、审计等服务，会导致应用服务越来越耦合。

本阶段补齐两个平台底座能力：

- 本地事件总线：用于应用内解耦。
- 工作单元：用于显式事务边界和提交后事件派发。

目标是在不影响现有功能的前提下，为后续复杂业务提供稳定扩展点。

## 功能范围

- 提供 `ILocalEvent`、`ILocalEventBus`、`ILocalEventHandler<TEvent>`。
- 提供 `IUnitOfWork`。
- 支持事件发布给多个 handler。
- 支持 handler 异常向上抛出。
- 支持显式事务开始、提交、回滚。
- 支持提交成功后派发 post-commit 事件。
- 支持回滚时清除 post-commit 事件。
- 在现有 DI 中注册事件总线和工作单元。

## 不做范围

- 不接 RabbitMQ、Kafka 等分布式消息。
- 不做 Outbox 表。
- 不强制改造现有 Repository。
- 不批量迁移已有应用服务。
- 不改变现有 `DbContext.SaveChangesAsync` 行为。

## 验收标准

- 本地事件可被多个 handler 按注册顺序消费。
- handler 异常不被反射包装吞掉。
- 无显式事务时，`SaveChangesAsync` 后派发 post-commit 事件。
- 显式事务中，`SaveChangesAsync` 不立即派发 post-commit 事件。
- `CommitAsync` 后派发 post-commit 事件。
- `RollbackAsync` 后不派发 post-commit 事件。
- 工作流和消息中心关键测试不受影响。

## 使用原则

- 简单 CRUD 可以继续使用现有 Repository。
- 跨多个聚合、多个写操作或需要提交后通知的场景，优先使用 `IUnitOfWork`。
- 需要解耦副作用时使用本地事件，例如清缓存、发通知、写扩展日志。
- 事件 handler 内要避免长事务和不可控外部依赖。
