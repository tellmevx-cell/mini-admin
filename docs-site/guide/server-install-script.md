# 单脚本服务器安装

本方式适合 Linux 或 1Panel 服务器：只上传 `mini-admin-server-install.sh`，脚本会从 Gitee 获取 `main` 分支，然后调用仓库内的一键部署器完成 Docker 构建、启动和健康检查。

## 部署前准备

服务器建议配置：

- Linux x86_64，推荐 4 核 8 GB，最低建议 2 核 4 GB。
- 至少 10 GB 可用磁盘，首次构建期间建议预留 20 GB。
- 已安装并启动 Docker、Docker Compose v2 和 Git。
- 国内服务器能够访问 `https://gitee.com` 和所配置的镜像/NuGet/npm 源。

1Panel 用户可先在 **应用商店 -> 已安装 -> Docker** 确认 Docker 正常，再打开 **主机 -> 终端**。

## 第一次安装

在本机仓库根目录找到：

```text
mini-admin-server-install.sh
```

通过 1Panel 上传到服务器 `/root`，然后执行：

```bash
cd /root
chmod +x mini-admin-server-install.sh
bash mini-admin-server-install.sh
```

默认会执行：

1. 从 `https://gitee.com/baijincom/mini-admin.git` 克隆 `main`。
2. 安装到 `/opt/mini-admin`。
3. 首次自动生成 `.env` 和随机 JWT、MySQL、Redis 密钥。
4. 串行构建 API、Gateway、Web 镜像。
5. 启动 MySQL、Redis、API、Gateway、Web。
6. 检查 API、代理链路、前端和 OIDC 发现文档。

部署成功后访问：

```text
http://服务器IP:5666
```

默认账号为 `admin / 123456`，首次登录后必须修改密码。

## 使用域名和 HTTPS

如果已经准备好 1Panel 域名，首次安装时直接传入公网地址：

```bash
MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/ \
MINIADMIN_WEB_PORT=127.0.0.1:5666 \
bash mini-admin-server-install.sh
```

然后在 1Panel 创建反向代理网站：

```text
代理地址：http://127.0.0.1:5666
```

申请并启用 HTTPS 后，确认 `/opt/mini-admin/.env` 包含：

```text
MINIADMIN_PUBLIC_ORIGIN=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ISSUER=https://admin.example.com/
MINIADMIN_OPEN_PLATFORM_ALLOW_INSECURE_HTTP=false
```

修改 `.env` 后在项目目录执行 `bash deploy.sh`。

## 后续更新

把新版安装脚本上传到 `/root` 后再次执行同一命令：

```bash
bash /root/mini-admin-server-install.sh
```

脚本只允许 Git 快进更新，不覆盖服务器仓库中的手工修改，并保留：

- `/opt/mini-admin/.env`。
- MySQL 数据卷。
- Redis 数据卷。
- 上传文件数据卷和 OIDC 签名证书。

如果要完全重新构建镜像：

```bash
bash /root/mini-admin-server-install.sh --no-cache
```

## 可选参数

```bash
# 指定其他安装目录
bash mini-admin-server-install.sh --dir /data/mini-admin

# 指定仓库或分支
bash mini-admin-server-install.sh \
  --repo https://gitee.com/baijincom/mini-admin.git \
  --branch main

# 部署成功后持续查看日志
bash mini-admin-server-install.sh --logs

# 已有镜像时跳过构建
bash mini-admin-server-install.sh --skip-build
```

也可以使用环境变量 `MINIADMIN_REPO_URL`、`MINIADMIN_BRANCH` 和 `MINIADMIN_INSTALL_DIR` 设置默认值。

## 部署后检查

```bash
cd /opt/mini-admin
docker compose ps
curl -fsS http://127.0.0.1:8080/health
curl -fsS http://127.0.0.1:8088/api/health
curl -fsS http://127.0.0.1:5666/api/health
curl -fsS http://127.0.0.1:5666/.well-known/openid-configuration
```

所有容器都应为 `healthy`，四条命令都应成功。

## 常见问题

### 安装目录已经存在但不是 Git 仓库

如果该目录来自离线压缩包，请进入目录直接执行：

```bash
cd /opt/mini-admin
bash deploy.sh
```

也可以使用 `--dir` 换一个空目录，安装器不会删除现有目录。

### 服务器仓库有手工修改

安装器会停止更新，避免覆盖配置或代码。`.env` 已被 Git 忽略，不会触发该保护；其他代码修改应先提交或备份，再重新执行。

### 无法访问 Gitee

改用 [Docker Compose 指南](./docker-compose.md#国内服务器无法访问-github) 中的离线部署包方案，在 Windows 本机运行 `scripts/package-server.ps1`，上传压缩包后执行 `bash deploy.sh`。

### 部署失败

仓库内的 `deploy.sh` 会输出失败阶段和相关容器日志。进一步排查：

```bash
cd /opt/mini-admin
docker compose ps
docker compose logs --tail=300 api gateway web mysql redis
```
