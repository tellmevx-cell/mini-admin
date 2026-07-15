# 平台内核 v1.0 — 服务端任务清单

全局约束：保持旧路由和数据库数据兼容；新能力默认关闭或回退旧行为；禁止在仓库中写入真实密钥；每阶段必须先有测试再移除兼容入口。

## 执行顺序

1. ✅ 平台核心项目与元数据契约
2. ✅ Dynamic API 与 Scalar
3. ✅ PageRegistry 与菜单同步
4. ✅ RBAC + ABAC 与授权快照
5. ✅ 版本化缓存与管理接口
6. ✅ 网关灰度、追踪和熔断
7. ✅ Scriban、SignalR 与聊天
8. ✅ OAuth2/OIDC 与 OpenAPI 凭证
9. ✅ 多存储、监控和国际化
10. ✅ 存量迁移、全量回归与部署验证

## 任务 1：平台核心项目与元数据契约 `✅ 已完成`

文件：`src/MiniAdmin.Platform.Core/*`、`src/MiniAdmin.Platform.AspNetCore/*`、`MiniAdmin.slnx`

- ✅ 定义 `DynamicApiAttribute`、方法和参数元数据。
- ✅ 定义 `PageDefinition`、`PermissionDefinition`、`IPageRegistry`。
- ✅ 定义 `AuthorizationRequest/Decision`、ABAC 条件树和缓存契约。
- ✅ 固化项目引用方向并添加架构测试。

## 任务 2：Dynamic API 与 Scalar `✅ 已完成`

文件：`src/MiniAdmin.Platform.AspNetCore/DynamicApi/*`、`src/MiniAdmin.Api/Program.cs`

- ✅ 使用 MVC ApplicationPart 发现 `[DynamicApi]` 应用服务。
- ✅ 根据中立元数据构建路由、HTTP 方法、绑定源和授权过滤器。
- ✅ 接入 Scalar，并迁移平台元数据、ABAC、缓存、聊天和开放平台服务作为样板。

## 任务 3：PageRegistry 与菜单同步 `✅ 已完成`

文件：`src/MiniAdmin.Infrastructure/Navigation/*`、`MiniAdminDatabaseInitializer.cs`

- ✅ 注册内置页面、权限和中英文标题。
- ✅ 启动时同步代码页面到菜单表，保留管理员自定义可见性和排序。
- ✅ 新模块统一输出 `PageDefinition`；已迁移生成模块并删除重复菜单种子。

## 任务 4：RBAC + ABAC `✅ 已完成`

文件：`src/MiniAdmin.Domain/Entities/AbacPolicy.cs`、`src/MiniAdmin.Infrastructure/Authorization/*`

- ✅ 新增策略实体、EF 配置、仓储与管理应用服务。
- ✅ 实现权限通配、显式拒绝、属性解析和可解释决策。
- ✅ 将现有 `RequirePermission` 委托给统一决策服务。

## 任务 5：版本化缓存 `✅ 已完成`

文件：`src/MiniAdmin.Infrastructure/Caching/PlatformCache.cs`

- ✅ 实现租户键空间、标签版本戳、键目录和容错读取。
- ✅ 迁移授权、菜单、配置和字典缓存。
- ✅ 在对应写路径提交后精准失效标签。

## 任务 6：网关流量治理 `✅ 已完成`

文件：`src/MiniAdmin.Gateway/*`

- ✅ 实现稳定哈希灰度选择、租户/请求头/白名单规则。
- ✅ 统一 TraceId 与代理响应头。
- ✅ 实现 Closed/Open/HalfOpen 断路器并保留现有限流。

## 任务 7：消息与实时通信 `✅ 已完成`

文件：`src/MiniAdmin.Application/UserNotifications/*`、`src/MiniAdmin.Api/Hubs/*`

- ✅ 将模板渲染迁移到 Scriban 并兼容旧占位符。
- ✅ 增加租户覆盖、短信通道与 SignalR 通知发布。
- ✅ 增加用户隔离的在线聊天和历史记录。

## 任务 8：开放平台 `✅ 已完成`

文件：`src/MiniAdmin.Api/OpenPlatform/*`、`src/MiniAdmin.Domain/Entities/OpenApiCredential.cs`

- ✅ 集成 OpenIddict 授权码、PKCE、刷新令牌和客户端凭证流程。
- ✅ 提供第三方应用注册、授权同意和撤销。
- ✅ 实现 AppKey/HMAC、时间窗、Nonce 防重放和作用域校验。

## 任务 9：基础设施适配 `✅ 已完成`

文件：`src/MiniAdmin.Infrastructure/Storage/*`、`SystemMonitor/*`、`Localization/*`

- ✅ 抽取 S3 Signature V4 公共实现并接入 S3/OSS/COS/MinIO。
- ✅ 增加 CPU、主板、磁盘、GPU、网络和运行时详情。
- ✅ 提供 zh-CN/en-US 文本资源和页面国际化解析。

## 任务 10：回归与交付 `✅ 已完成`

- ✅ 全量后端测试、前端类型检查和生产构建。
- ✅ 更新文档站、配置示例、Docker 健康检查和一键部署。
- ✅ 删除重复的旧端点、菜单种子和误生成的 EF 迁移历史业务模块。

验收结果：Release 构建通过；后端 `265/265` 测试通过；前端类型检查和生产构建通过；部署脚本 Shell 语法校验通过。当前 Windows 验收机未安装 Docker，容器实际启动需在 Linux/1Panel 目标机执行 `bash deploy.sh` 完成最后环境验收。
