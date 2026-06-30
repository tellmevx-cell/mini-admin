# 部署上线

本页说明 MiniAdmin 从开发环境走向部署环境时需要检查的内容。

## 部署产物

MiniAdmin 一般包含三类产物：

| 产物 | 来源 | 说明 |
| --- | --- | --- |
| 后端 API | `src/MiniAdmin.Api` | ASP.NET Core 服务 |
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

仓库提供了 `docker-compose.yml`、`Dockerfile.api`、前端 Nginx 镜像配置和 `.env.example`。它适合本机体验、内网演示和小规模部署基线：

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
