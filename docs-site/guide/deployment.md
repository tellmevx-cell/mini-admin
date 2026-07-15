# 部署上线

本页说明 MiniAdmin 从开发环境走向部署环境时需要检查的内容。

## 部署产物

MiniAdmin 一般包含三类产物：

| 产物 | 来源 | 说明 |
| --- | --- | --- |
| 后端 API | `src/MiniAdmin.Api` | ASP.NET Core 服务 |
| 网关 | `src/MiniAdmin.Gateway` | YARP 统一入口和反向代理 |
| 前端静态资源 | `frontend/vue-vben-admin/apps/web-antd` | Vben Ant Design Vue 应用 |
| 文档站 | `docs-site` | VitePress 静态站点 |

## 后端发布

```powershell
dotnet publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/api
```

发布前确认：

- `appsettings.json` 中没有生产密钥明文。
- 生产配置通过环境变量、密钥服务或独立配置文件注入。
- JWT 密钥、数据库连接、Redis、文件存储、邮件和 Webhook 配置完整。
- 数据库迁移策略明确。

## 网关发布

```powershell
dotnet publish src/MiniAdmin.Gateway/MiniAdmin.Gateway.csproj -c Release -o artifacts/gateway
```

默认开发上游是 `http://localhost:5021/`。生产环境建议通过环境变量指定 API 地址：

```powershell
$env:ReverseProxy__Clusters__miniadmin_api__Destinations__api__Address = "http://your-api-inner-host:8080/"
```

网关默认暴露：

- `/health`：网关自身健康检查。
- `/api/**`：转发到 API，并移除 `/api` 前缀。

更多说明见 [网关与微服务演进](./gateway-microservices.md)。

## 前端构建

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd build
```

构建产物位于：

```text
frontend/vue-vben-admin/apps/web-antd/dist
```

上线前确认：

- API 基地址指向正确后端。
- 登录、动态菜单、权限按钮能正常使用。
- 生产环境没有开发代理依赖。

## 文档站构建

```powershell
pnpm docs:build
```

构建产物位于：

```text
docs-site/.vitepress/dist
```

可以部署到任意静态站点服务，例如 Nginx、OSS 静态网站、Git Pages 或内部文档平台。

## Docker Compose 部署

仓库提供了 `docker-compose.yml`、`Dockerfile.api`、`Dockerfile.gateway`、前端 Nginx 镜像配置和 `.env.example`。它适合本机体验、内网演示和小规模部署基线：

```bash
bash scripts/deploy-mini-admin.sh
```

脚本会自动生成 `.env`、校验 Compose、构建镜像、启动容器并检查健康状态。它适合 1Panel 终端和普通 Linux 服务器。

也可以手动执行：

```powershell
Copy-Item .env.example .env
docker compose config
docker compose up -d --build
```

上线前必须修改 `.env` 中的 JWT、MySQL、Redis 密码，并把 `.env` 保留在服务器本地，不要提交到仓库。完整说明见 [Docker Compose 指南](./docker-compose.md)。

## 接口限流配置

MiniAdmin 现在有两层限流：网关层和 API 层。

网关层保护统一入口，默认配置位于 `src/MiniAdmin.Gateway/appsettings.json`。它主要负责全局请求限流和登录接口限流。Docker 环境可以用这些变量覆盖：

```bash
MINIADMIN_GATEWAY_RATE_LIMITING_ENABLED=true
MINIADMIN_GATEWAY_RATE_LIMITING_PERMIT_LIMIT=1200
MINIADMIN_GATEWAY_RATE_LIMITING_WINDOW_SECONDS=60
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_PERMIT_LIMIT=20
MINIADMIN_GATEWAY_LOGIN_RATE_LIMITING_WINDOW_SECONDS=60
```

API 层内置 ASP.NET Core RateLimiter，用于保护后台接口、登录接口和文件上传接口，是绕过网关或内部调用时的应用层兜底。

默认配置位于 `src/MiniAdmin.Api/appsettings.json`：

| 配置项 | 默认值 | 说明 |
| --- | ---: | --- |
| `RateLimiting:Enabled` | `true` | 是否启用后端限流 |
| `RateLimiting:PermitLimit` | `600` | 全局每个用户或 IP 在窗口期内允许的请求数 |
| `RateLimiting:WindowSeconds` | `60` | 全局固定窗口秒数 |
| `RateLimiting:QueueLimit` | `0` | 全局限流排队数量，默认不排队 |
| `RateLimiting:LoginPermitLimit` | `10` | 登录接口每个 IP 在窗口期内允许的请求数 |
| `RateLimiting:LoginWindowSeconds` | `60` | 登录接口固定窗口秒数 |
| `RateLimiting:LoginQueueLimit` | `0` | 登录接口限流排队数量，默认不排队 |
| `RateLimiting:UploadPermitLimit` | `4` | 文件上传接口每个用户或 IP 的并发上传数 |
| `RateLimiting:UploadQueueLimit` | `0` | 上传并发超限后的排队数量，默认不排队 |

触发限流时，接口返回 `HTTP 429 Too Many Requests`，响应体仍使用统一的 `ApiResponse` 格式，并尽量带上 `Retry-After` 响应头。

Docker 或 1Panel 环境建议用环境变量覆盖，例如：

```bash
RateLimiting__Enabled=true
RateLimiting__PermitLimit=600
RateLimiting__WindowSeconds=60
RateLimiting__LoginPermitLimit=10
RateLimiting__LoginWindowSeconds=60
RateLimiting__UploadPermitLimit=4
```

如果前面还有 Nginx、CDN 或 1Panel 反向代理，建议保留网关限流，并在最外层代理补充基础访问频率限制。API 层限流是应用层兜底，登录前主要按远端 IP 限流，登录后按用户限流。

## 数据库准备

生产环境建议使用 MySQL。

上线前确认：

- 数据库账号权限最小化。
- 连接串不写入仓库。
- 已执行 EF Core Migrations 或初始化脚本。
- 初始化种子数据完成。
- 管理员账号、角色、菜单和租户数据可用。

## 文件存储准备

MiniAdmin 抽象了文件存储，当前可使用本地存储或 MinIO。

本地存储适合开发和小规模部署。生产环境更推荐对象存储或 MinIO，并确认：

- 存储路径或桶存在。
- 服务账号有读写权限。
- 文件大小限制符合业务需要。
- 文件访问策略符合安全要求。

## 消息通道准备

站内信不依赖外部服务。邮件和 Webhook 需要额外配置。

上线前确认：

- 通知策略中默认启用站内信。
- 邮件配置完整后再启用邮件通道。
- Webhook 地址、鉴权和网络可达。
- 投递失败告警能被管理员看到。

## 上线验收

建议至少完成：

- 登录、退出、刷新 token 或会话保持。
- 用户、角色、菜单、权限诊断。
- 租户创建、启用、禁用。
- 工作流发起、同意、驳回、撤回、抄送已读。
- 消息中心站内信、模板、策略、订阅和投递记录。
- 审计日志、登录日志、系统监控。
- 文件上传和下载。

详细清单见 [验收清单](../runbooks/acceptance.md)。
