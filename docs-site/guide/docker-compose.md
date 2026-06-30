# Docker Compose

本页说明如何用 Docker Compose 一次性启动 MiniAdmin 的前端、后端、MySQL 和 Redis。

这种方式适合：

- 本地快速体验完整依赖。
- 给团队或客户搭一个内网演示环境。
- 作为生产部署前的容器化参考。

如果你只是二开后端或前端，仍然可以使用 [快速开始](./quick-start.md) 中的本地开发方式。

## 文件说明

| 文件 | 作用 |
| --- | --- |
| `.env.example` | Compose 环境变量模板，不包含真实密钥 |
| `docker-compose.yml` | 编排 MySQL、Redis、API、Web |
| `Dockerfile.api` | 构建并运行 ASP.NET Core API |
| `frontend/vue-vben-admin/apps/web-antd/Dockerfile` | 构建前端并用 Nginx 托管 |
| `frontend/vue-vben-admin/apps/web-antd/nginx.conf` | 前端静态资源与 `/api` 反向代理配置 |

## 准备环境变量

在仓库根目录执行：

```powershell
Copy-Item .env.example .env
```

然后编辑 `.env`，至少替换这些值：

```text
MINIADMIN_JWT_SIGNING_KEY=replace_with_a_long_random_signing_key_change_me
MINIADMIN_MYSQL_PASSWORD=replace_mysql_password_change_me
MINIADMIN_MYSQL_ROOT_PASSWORD=replace_mysql_root_password_change_me
MINIADMIN_REDIS_PASSWORD=replace_redis_password_change_me
```

建议使用长度足够的随机字符串。`.env` 已被 `.gitignore` 忽略，不应提交到仓库。

## 一键部署脚本

如果你使用 1Panel，推荐先进入 1Panel 的 `主机 -> 终端`，拉取代码后直接执行仓库内脚本：

```bash
cd /opt/mini-admin
bash scripts/deploy-mini-admin.sh
```

脚本会自动完成：

- 检查 Docker 和 Docker Compose 是否可用。
- 如果 `.env` 不存在，生成随机 JWT、MySQL 和 Redis 密码。
- 如果 `.env` 仍是示例占位值，先备份再重新生成。
- 执行 `docker compose config` 校验配置。
- 执行 `docker compose up -d --build --remove-orphans` 构建并启动。
- 检查 API 健康地址和前端访问地址。

默认情况下，脚本会把 API 端口绑定为 `127.0.0.1:8080`，只允许服务器本机访问；前端端口为 `5666`，用于浏览器访问或 1Panel 网站反向代理。

常用参数：

```bash
# 拉取最新代码后部署
bash scripts/deploy-mini-admin.sh --pull

# 不重新构建镜像，只启动容器
bash scripts/deploy-mini-admin.sh --skip-build

# 备份并重新生成 .env
bash scripts/deploy-mini-admin.sh --force-env

# 部署完成后持续查看日志
bash scripts/deploy-mini-admin.sh --logs

# 修改前端访问端口
MINIADMIN_WEB_PORT=8088 bash scripts/deploy-mini-admin.sh

# 国内服务器前端依赖下载慢时，进一步降低并发并拉长超时
MINIADMIN_PNPM_NETWORK_CONCURRENCY=2 MINIADMIN_PNPM_FETCH_TIMEOUT=900000 bash scripts/deploy-mini-admin.sh
```

1Panel 绑定域名时，可以创建反向代理网站，代理地址填写：

```text
http://127.0.0.1:5666
```

## 启动

先校验 Compose 配置：

```powershell
docker compose config
```

构建并启动：

```powershell
docker compose up -d --build
```

查看服务状态：

```powershell
docker compose ps
```

访问地址：

```text
前端：http://localhost:5666
后端健康检查：http://localhost:8080/health
```

默认账号：

| 场景 | 租户编码 | 用户名 | 密码 |
| --- | --- | --- | --- |
| 平台管理员 | 留空 | `admin` | `123456` |
| 演示租户 | `demo` | `demo` | `123456` |

首次部署后请尽快修改默认密码。

## 查看日志

查看全部日志：

```powershell
docker compose logs -f
```

只看后端：

```powershell
docker compose logs -f api
```

只看前端 Nginx：

```powershell
docker compose logs -f web
```

## 数据和文件

Compose 默认使用具名卷：

| 卷 | 内容 |
| --- | --- |
| `miniadmin_mysql` | MySQL 数据 |
| `miniadmin_redis` | Redis 数据 |
| `miniadmin_uploads` | 后端本地上传文件 |

停止容器但保留数据：

```powershell
docker compose down
```

停止并删除数据卷：

```powershell
docker compose down -v
```

执行 `down -v` 会删除数据库、Redis 数据和上传文件，请只在确认不需要这些数据时使用。

## 常见问题

### docker compose config 提示变量缺失

确认根目录存在 `.env`，并且已经设置：

```text
MINIADMIN_JWT_SIGNING_KEY
MINIADMIN_MYSQL_PASSWORD
MINIADMIN_MYSQL_ROOT_PASSWORD
MINIADMIN_REDIS_PASSWORD
```

### 前端能打开但接口失败

前端容器会把 `/api/` 代理到 `mini-admin-api:8080`。检查 API 是否健康：

```powershell
docker compose ps api
docker compose logs api
```

也可以直接访问：

```text
http://localhost:8080/health
```

### MySQL 或 Redis 一直不健康

优先检查密码变量是否为空，以及端口和卷是否被旧环境占用：

```powershell
docker compose logs mysql
docker compose logs redis
```

如果是本地试验环境且不需要旧数据，可以执行：

```powershell
docker compose down -v
docker compose up -d --build
```

### 前端构建较慢

前端基于 Vben workspace，第一次 `pnpm install` 和构建会比较慢。后续 Docker 层缓存命中后会快很多。

如果国内服务器在 `pnpm install --frozen-lockfile` 阶段下载 `@iconify/json`、`echarts` 等大包超时，可以在 `.env` 中调整：

```text
MINIADMIN_NPM_REGISTRY=https://registry.npmmirror.com
MINIADMIN_PNPM_FETCH_TIMEOUT=900000
MINIADMIN_PNPM_FETCH_RETRIES=8
MINIADMIN_PNPM_NETWORK_CONCURRENCY=2
```

然后重新构建：

```bash
docker compose build --no-cache web
docker compose up -d
```

## 生产提醒

Compose 文件是可运行基线，不等于完整生产方案。生产环境建议补充：

- HTTPS 证书和统一反向代理。
- 数据库和 Redis 的备份策略。
- 日志采集和监控告警。
- 更严格的 CORS、网络访问控制和密钥管理。
- 管理员默认密码、演示账号和 JWT 密钥轮换。
