# 平台内核

平台内核把接口、权限、菜单、缓存和网关治理收敛到统一元数据，避免一个业务模块分别维护 Controller、路由、菜单种子和权限清单。

## 管理入口

管理员登录后可从左侧菜单进入 **平台内核**：

| 页面 | 路由 | 用途 |
| --- | --- | --- |
| 访问控制策略 | `/platform-kernel/access-control` | 管理 RBAC 之上的 ABAC 允许/拒绝策略 |
| 缓存管理 | `/platform-kernel/cache` | 查询当前节点缓存目录、按标签或逻辑键失效 |
| 在线聊天 | `/message/chat` | 用户会话、历史消息、未读数和实时已读回执 |
| 开放平台应用 | `/open-platform/applications` | OAuth 客户端与个人 OpenAPI 凭证 |

页面首次出现依赖启动时的 PageRegistry 同步。已有数据库升级后重启 API，再重新登录即可刷新动态菜单。

## RBAC 与 ABAC

RBAC 先确认用户是否拥有权限码，ABAC 再结合用户、租户、请求、资源属性做细粒度决策。显式 `Deny` 优先于 `Allow`，没有匹配策略时保持原 RBAC 行为。

策略关键字段：

| 字段 | 示例 | 说明 |
| --- | --- | --- |
| 主体类型 | `Any`、`User`、`Role`、`Application` | 策略约束对象 |
| 资源 | `business.customer` | 与 Dynamic API 的 `Resource` 一致 |
| 动作 | `query`、`create`、`delete` | 支持 `*` 通配 |
| 效果 | `Allow`、`Deny` | 拒绝优先 |
| 优先级 | `100` | 数值越大越先评估 |

条件使用受控 JSON 树，不执行脚本。例如只允许公司网段查询：

```json
{
  "all": [
    { "attribute": "request.method", "operator": "equals", "value": "GET" },
    { "attribute": "request.ip", "operator": "ipInCidr", "value": "10.20.0.0/16" }
  ]
}
```

支持 `all`、`any`、`not` 组合，以及 `equals`、`notEquals`、`contains`、`in`、`startsWith`、`endsWith`、数值比较和 `ipInCidr` 等操作。保存前后端都会校验 JSON。

## 版本化分布式缓存

授权快照、菜单、系统参数和字典统一使用 `IPlatformCache`。缓存键包含租户命名空间，并通过标签版本门控实现跨节点精准失效，不依赖 Redis `SCAN`。

管理页支持：

- 按分类查看当前节点已知逻辑键。
- 按一个或多个标签递增版本，使相关旧缓存自然失效。
- 按分类、逻辑键和租户移除单项缓存。

生产环境使用 Redis 时，失效版本对所有 API 实例生效；键目录仅表示当前节点已观察到的条目，不等同于 Redis 全库浏览器。

## Dynamic API 与 Scalar

应用服务通过 `[DynamicApi]` 和 `[DynamicGet/Post/Put/Delete]` 自动成为接口，并携带权限、ABAC 资源动作、OpenAPI OperationId 和说明。接口文档入口：

```text
http://localhost:5021/scalar
http://localhost:5021/openapi/v1.json
```

业务校验异常统一返回 `400`，资源不存在返回 `404`，越权返回 `403`；未知异常仍进入全局日志。

## 网关治理

`MiniAdmin.Gateway` 在 YARP 上提供：

- 按百分比、租户、用户、IP、请求头进行稳定哈希灰度。
- `X-Trace-Id` 请求追踪与响应回传。
- 全局与登录接口限流。
- Closed/Open/HalfOpen 断路器。
- `/api/**`、`/.well-known/**`、`/connect/**` 代理。

默认灰度关闭，稳定与灰度目标都指向同一个 API，因此不改变现有部署行为。只有准备好第二个 API 实例后，才设置 `MINIADMIN_CANARY_API_ADDRESS` 和灰度比例。

## 基础设施能力

- 文件存储统一支持 Local、S3、OSS、COS 与 MinIO，云端实现复用 S3 Signature V4。
- 系统监控展示主板、CPU、内存、磁盘、GPU、网络接口和 .NET 运行时信息。
- PageRegistry 同时保存中英文标题，菜单元数据按 `Accept-Language` 解析。
- Scriban 模板支持全局默认与租户覆盖，通知和聊天通过 SignalR 实时推送。

二开写法见 [Dynamic API 与 PageRegistry](../developer/dynamic-api-page-registry.md)。
