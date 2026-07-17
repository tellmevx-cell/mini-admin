# 租户资源用量看板与配额预警设计

## 总体方案

沿用现有分层和动态 API 机制：应用层只暴露统一 DTO 与查询入口；基础设施层负责实时聚合、状态机、通知和持久化；定时任务复用现有执行器；前端在工作台和平台租户列表消费同一套状态语义。

## 数据模型

新增 `TenantResourceQuotaWarning`：

| 字段 | 说明 |
| --- | --- |
| `Id` | 主键 |
| `TenantId` | 租户标识 |
| `ResourceType` | `Users` 或 `Storage` |
| `Status` | `Unlimited`、`Normal`、`Warning`、`Exhausted` |
| `UsedValue` | 最近扫描使用量 |
| `LimitValue` | 最近扫描上限 |
| `NotificationSequence` | 告警轮次，用于生成幂等通知来源 |
| `LastNotifiedStatus` | 最近已通知状态 |
| `LastNotifiedAt` | 最近通知时间 |
| `LastCheckedAt` | 最近扫描时间 |

唯一索引：`TenantId + ResourceType`。迁移不创建到 `mini_tenants` 的外键，兼容当前租户表初始化顺序；业务服务保证租户有效性。

## 服务契约

- `GetCurrentUsageAsync`：返回当前租户的套餐、两类资源指标和整体状态；平台上下文返回空。
- `ScanAsync`：扫描有效租户，更新状态，按状态跃迁创建通知，并返回任务汇总与异常明细。
- `TenantResourceUsageAppService`：通过 `[DynamicApi]` 暴露 `GET /api/tenant/resource-usage`，仅要求登录，不新增菜单权限。

## 状态机

```text
Unlimited <- 上限为 0
Normal    <- 使用率 < 80%
Warning   <- 80% <= 使用率 < 100%
Exhausted <- 使用率 >= 100%
```

通知条件为当前状态属于 `Warning` 或 `Exhausted`，且最近已通知状态与当前状态不同。进入 `Normal` 或 `Unlimited` 时清空最近已通知状态，使下一轮风险跃迁可以再次通知。

## 通知设计

- 默认模板代码：`TenantQuota.Warning`。
- 分类：`TenantQuota`。
- 级别：`Warning` 或 `Critical`。
- 接收人：当前租户内启用且角色为 `tenant-admin` 的用户。
- 来源类型：`TenantQuota`。
- 来源 ID：`{tenantId:N}:{resourceCode}:{sequence}`，满足消息唯一索引长度限制。
- 链接：用户资源跳转用户管理，存储资源跳转文件管理。
- 创建通知后复用现有仓储自动发布 SignalR 消息和未读数量。

## 定时任务

- 任务键：`tenant-resource-quota-warning`。
- 默认周期：每天一次。
- 支持在定时任务页面手动执行。
- 有预警或耗尽租户时任务状态为 `Warning`，并为每个异常资源生成执行明细。

## 前端设计

### 租户工作台

- 顶部新增资源概览区，展示套餐名和最后检查时间。
- 用户、存储各使用一张状态卡，包含数值、进度条、状态说明和管理入口。
- 风险状态使用金色或红色强调，不限额明确显示“无限制”。
- 平台管理员不显示此区域，不影响原工作台内容。

### 平台租户列表

- 现有资源用量进度条按状态着色。
- 展示预警状态标签和最近通知时间。

## 兼容与风险控制

- 不改现有配额拦截和事务锁，预警扫描为只读聚合加独立状态写入。
- 状态保存与通知创建在同一服务作用域中，通知唯一索引兜底并发幂等。
- MySQL 使用增量迁移；`EnsureCreated` 和旧库通过兼容建表方法补齐。
- InMemory 环境不执行数据库专用 SQL，测试可稳定运行。
