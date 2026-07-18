# Docker Compose 一键部署

MiniAdmin 提供可直接用于 Linux 和 1Panel 的 Docker Compose 部署方案，一次启动 MySQL、Redis、API、YARP Gateway 和 Web。

推荐入口只有一条：

```bash
bash deploy.sh
```

脚本会生成随机生产密钥、构建镜像、按依赖顺序启动服务、等待数据库迁移和种子数据完成，并验证 `Web -> Gateway -> API` 完整链路。任何阶段失败都会停止部署并显示对应日志。

## 部署拓扑

```text
浏览器 / 1Panel HTTPS
          |
          v
      Web :5666
          |
       /api/*
          v
    Gateway :8088
          |
          v
      API :8080
       /       \
    MySQL     Redis
```

- 公网只需要暴露 Web，API 和 Gateway 默认绑定 `127.0.0.1`。
- MySQL 和 Redis 没有宿主机端口，只能通过 Docker 内部网络访问。
- Web、Gateway、API、MySQL、Redis 都有独立健康检查。
- 容器日志默认轮转，单文件最大 20 MB，保留 3 个文件。

## 文件说明

| 文件 | 作用 |
| --- | --- |
| `mini-admin-server-install.sh` | 单独上传到服务器，从 Gitee 安装或安全更新 `main` 后执行部署 |
| `deploy.sh` | 服务器一键入口 |
| `scripts/deploy-mini-admin.sh` | 部署、重试、健康检查和故障诊断实现 |
| `scripts/backup-mini-admin.sh` | 一致性备份 MySQL、上传卷、配置清单与校验和 |
| `scripts/restore-mini-admin.sh` | 校验、恢复、清缓存并等待全栈健康 |
| `scripts/package-server.ps1` | 在 Windows 本机生成安全的服务器上传包 |
| `docker-compose.yml` | 五个服务、网络、数据卷和健康检查 |
| `.env.example` | 环境变量模板，不包含真实密钥 |
| `Dockerfile.api` | 构建 ASP.NET Core API |
| `Dockerfile.gateway` | 构建 YARP Gateway |
| `frontend/vue-vben-admin/apps/web-antd/Dockerfile` | 构建前端并用 Nginx 托管 |

## 服务器可访问代码仓库

如果只想先上传一个文件，优先使用 [单脚本服务器安装](./server-install-script.md)。它会从 Gitee 克隆或安全更新仓库，再自动调用本页的 `deploy.sh`。

进入项目目录后执行：

```bash
cd /opt/mini-admin
bash deploy.sh
```

以后更新代码并重新部署：

```bash
cd /opt/mini-admin
bash deploy.sh --pull
```

首次部署通常需要下载基础镜像、NuGet 包和 pnpm 包，耗时取决于服务器网络。下载内容使用 Docker BuildKit 缓存，网络中断后重新执行脚本会复用已经下载的内容。

## 国内服务器无法访问 GitHub

这种情况不需要让服务器拉仓库。在 Windows 开发机的仓库根目录执行：

```powershell
powershell -ExecutionPolicy Bypass -File scripts/package-server.ps1
```

输出文件位于：

```text
artifacts/deploy/mini-admin-server-<commit>.tar.gz
```

部署包通过 `git archive` 生成，只包含当前已提交版本，不会带上服务器根 `.env`、被忽略的 `appsettings.Development.json`、本地日志、上传文件和构建产物。脚本还会扫描归档文件名，一旦发现生产 `.env`、本地 appsettings、证书或私钥就直接终止。如果工作区有未提交修改，脚本会警告，这些修改不会进入部署包。

在 1Panel 中完成以下操作：

1. 打开 `主机 -> 文件`，把压缩包上传到 `/opt`。
2. 打开 `主机 -> 终端`，解压并进入目录。
3. 执行一键部署脚本。

```bash
cd /opt
tar -xzf mini-admin-server-*.tar.gz
cd /opt/mini-admin
bash deploy.sh
```

更新版本时上传新的压缩包，再次解压到 `/opt` 并执行 `bash deploy.sh`。服务器已有的 `.env` 和 Docker 数据卷不会包含在压缩包中，也不会被覆盖。

## 一键脚本做了什么

脚本按以下顺序工作：

1. 检查 Docker、Docker Compose、磁盘和可用内存。
2. 首次部署时生成 `.env`，JWT、MySQL、Redis 使用随机密钥。
3. 校验完整 Compose 配置。
4. 依次构建 API、Gateway、Web，失败自动重试并复用依赖缓存。
5. 启动 MySQL 和 Redis，等待二者健康。
6. 启动 API，最多等待 8 分钟完成迁移和初始化。
7. 启动 Gateway 和 Web。
8. 验证 API、Gateway、Web 以及 `/api/health` 完整代理链路。
9. 验证经 Web 入口访问 OIDC 发现文档。

常用参数：

```bash
# 正常部署或更新
bash deploy.sh

# Git 仓库模式下先拉取再部署
bash deploy.sh --pull

# 镜像层异常时强制重新构建
bash deploy.sh --no-cache

# 已有镜像时只重启容器
bash deploy.sh --skip-build

# 成功后持续查看日志
bash deploy.sh --logs

# 修改 Web 端口
MINIADMIN_WEB_PORT=8090 bash deploy.sh

# 只允许 1Panel/Nginx 从服务器本机访问 Web
MINIADMIN_WEB_PORT=127.0.0.1:5666 bash deploy.sh
```

`--force-env` 会尝试重新生成密钥。检测到已有 MySQL 数据卷时脚本会拒绝执行，因为直接更换 `.env` 中的数据库密码会导致旧数据卷无法登录。

## 1Panel 配置域名

部署成功后，在 1Panel 创建反向代理网站：

```text
代理地址：http://127.0.0.1:5666
```

然后在 1Panel 申请并启用 HTTPS 证书。使用域名反向代理时，建议在 `.env` 中把 Web 端口改为：

```text
MINIADMIN_WEB_PORT=127.0.0.1:5666
```

修改后重新执行：

```bash
bash deploy.sh
```

同时把 `.env` 中的公网来源配置为该 HTTPS 域名，保留尾部 `/`：

```text
MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ISSUER=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=false
MINIADMIN_TRUST_FORWARDED_HEADERS=true
```

启用转发头信任时，API 与 Gateway 必须保持回环绑定；HTTPS 场景还应让 1Panel/Nginx 代理回环 Web 端口，避免公网客户端直接伪造代理头。

API 会在持久化上传卷中生成 OIDC RSA 签名证书。不要删除该卷；多 API 实例部署时必须共享同一证书。

## 灰度与网关治理

单实例部署无需开启灰度，稳定和灰度目标默认都指向 `api:8080`。部署第二个 API 实例后再设置：

```text
MINIADMIN_GATEWAY_CANARY_ENABLED=true
MINIADMIN_GATEWAY_CANARY_PERCENTAGE=10
MINIADMIN_CANARY_API_ADDRESS=http://api-canary:8080/
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_ENABLED=true
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_FAILURE_THRESHOLD=5
MINIADMIN_GATEWAY_CIRCUIT_BREAKER_BREAK_SECONDS=30
```

请求会携带并返回 `X-Trace-Id`。灰度目标未准备好时不要只修改比例，否则网关会把部分流量发送到不可达地址。

## 国内镜像与网络配置

脚本生成的 `.env` 默认降低 pnpm 并发、增加重试和超时时间：

```text
MINIADMIN_NPM_REGISTRY=https://registry.npmmirror.com
MINIADMIN_PNPM_FETCH_TIMEOUT=900000
MINIADMIN_PNPM_FETCH_RETRIES=8
MINIADMIN_PNPM_NETWORK_CONCURRENCY=2
```

如果服务器配置了企业镜像仓库或 1Panel 镜像加速，可以覆盖所有基础镜像：

```text
MINIADMIN_MYSQL_IMAGE=mysql:8.4
MINIADMIN_REDIS_IMAGE=redis:7.4-alpine
MINIADMIN_DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0
MINIADMIN_DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0
MINIADMIN_NODE_IMAGE=node:24-alpine
MINIADMIN_NGINX_IMAGE=nginx:1.27-alpine
```

把右侧值改成镜像仓库中的完整镜像地址即可，不需要修改 Dockerfile。

## 日常运维

```bash
cd /opt/mini-admin

# 查看状态
docker compose ps

# 查看关键日志
docker compose logs -f api
docker compose logs -f gateway
docker compose logs -f web

# 重启全部服务
docker compose restart

# 停止并保留数据
docker compose down

# 再次部署
bash deploy.sh

# 生产备份（包含上传卷时会短暂停止并自动恢复 API）
bash scripts/backup-mini-admin.sh
```

数据保存在三个具名卷中：

| 数据卷 | 内容 |
| --- | --- |
| `miniadmin_mysql` | MySQL 业务数据 |
| `miniadmin_redis` | Redis 持久化数据 |
| `miniadmin_uploads` | 本地文件存储 |

`docker compose down` 不会删除这些数据。`docker compose down -v` 会永久删除数据库、Redis 和上传文件，只能在确认不需要任何旧数据的全新测试环境中使用。

备份、恢复、定时策略和故障处置的完整步骤见[生产可靠性运行手册](../runbooks/production-reliability.md)。备份保存在同一台服务器上不算完整灾备，必须额外同步到异机或对象存储。

## 常见故障

### 前端依赖下载超时

直接重新执行 `bash deploy.sh`。pnpm 下载目录使用 BuildKit 缓存，已完成的包不会从零重新下载。仍然不稳定时，在 `.env` 中进一步设置：

```text
MINIADMIN_PNPM_FETCH_TIMEOUT=1200000
MINIADMIN_PNPM_FETCH_RETRIES=10
MINIADMIN_PNPM_NETWORK_CONCURRENCY=1
```

前端构建镜像已经安装 Git，不会再因 `lefthook install` 报 `git: executable file not found`。

### API 显示 unhealthy

一键脚本会自动打印 API、MySQL、Redis 的最近日志。也可以手工执行：

```bash
docker compose ps
docker compose logs --tail=300 api mysql redis
```

首次初始化会创建表、执行迁移并写入菜单权限种子数据，脚本和健康检查都预留了足够时间。如果日志来自旧版本并出现 `mini_role_menus` 外键错误，请先上传最新代码再执行 `bash deploy.sh`，当前初始化逻辑已经跳过不存在的菜单引用。

### 数据库密码不匹配

MySQL 数据卷创建后，单纯修改 `.env` 不会修改数据卷内账号密码。已有业务数据时应恢复原 `.env` 或在 MySQL 内正式轮换密码。

如果只是第一次失败安装，确认没有任何数据需要保留后，才可以重建：

```bash
docker compose down -v
bash deploy.sh --force-env
```

### 端口被占用

查看占用：

```bash
ss -lntp | grep -E ':5666|:8080|:8088'
```

修改 `.env` 中对应端口后重新部署，不需要修改 Compose 文件。

### 构建时内存不足

4 核 8 GB 服务器可以部署。首次前端构建建议至少保留 4 GB 可用内存和 8 GB 可用磁盘；脚本会在资源偏低时提前警告，并强制按服务串行构建。

## 上线检查

- 首次登录后立即修改 `admin` 和 `demo` 默认密码。
- `.env` 权限应为 `600`，不要上传到 Git 或发送给他人。
- 生产环境通过 1Panel/Nginx 启用 HTTPS，只公开 80/443。
- HTTPS 部署必须关闭 `MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP`，并确认 OIDC 发现文档中的 issuer 是公网域名。
- 定期执行 `scripts/backup-mini-admin.sh`，异地保存并完成恢复演练。
- 更新前先备份数据库，更新后检查 `docker compose ps` 和 API 日志。
