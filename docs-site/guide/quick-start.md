# 快速开始

本页用于让第一次接触 MiniAdmin 的开发者在本地把前后端跑起来。

## 环境要求

| 工具 | 建议版本 |
| --- | --- |
| .NET SDK | 10.x，项目 TargetFramework 为 `net10.0` |
| Node.js | 22.x 或 24.x，前端 Vben workspace 要求 `^22.18.0 || ^24.0.0` |
| pnpm | 10.x 或 11.x |
| MySQL | 可选。本地快速体验可先使用 InMemory |
| Redis | 可选。没有 Redis 时可走内存缓存或降级逻辑 |

## 获取代码

```powershell
git clone <your-repo-url>
cd mini-admin
```

如果你已经在当前仓库中开发，可以直接从仓库根目录执行后续命令。

## 启动后端

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

健康检查：

```powershell
Invoke-WebRequest -Uri http://localhost:5021/health -UseBasicParsing
```

默认配置会初始化基础菜单、权限、管理员、系统数据、工作流示例和通知策略。快速体验时建议先使用默认 InMemory 模式，减少数据库配置干扰。

如果你想同时体验 MySQL、Redis、API 和前端容器，可以直接走 [Docker Compose 指南](./docker-compose.md)。

## 启动前端

```powershell
cd frontend/vue-vben-admin
pnpm install
pnpm run dev:antd
```

默认访问地址：

```text
http://localhost:5666
```

默认账号：

```text
admin / 123456
```

演示租户账号：

```text
tenant: demo
user: demo
password: 123456
```

## 启动文档站

回到仓库根目录：

```powershell
pnpm --dir docs-site install
pnpm docs:dev
```

也可以进入文档目录执行：

```powershell
cd docs-site
pnpm dev
```

## 第一次打开后检查

登录后建议按顺序检查：

1. 左侧菜单是否正常加载。
2. 用户、角色、菜单是否能打开。
3. 工作流审批中心是否能打开。
4. 顶部铃铛和消息中心是否能打开。
5. 系统监控、审计日志是否能打开。

如果菜单为空，优先退出后重新登录。如果仍然为空，检查当前用户角色是否分配了菜单权限。

## 常见启动问题

### 后端端口被占用

换一个端口启动：

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5022
```

同时调整前端代理配置。

### DLL 被占用

通常是之前的 `MiniAdmin.Api` 进程还在运行：

```powershell
Get-Process MiniAdmin.Api -ErrorAction SilentlyContinue
Stop-Process -Name MiniAdmin.Api -ErrorAction SilentlyContinue
```

### 前端 Node 版本不匹配

前端 Vben workspace 对 Node 版本有要求。确认：

```powershell
node -v
pnpm -v
```

如果版本不符合，先切换 Node 版本，再重新安装依赖。

### 接口 404 或无响应

确认后端地址和前端代理一致。当前常用后端地址是：

```text
http://localhost:5021
```

### Docker 方式启动失败

推荐直接重新执行一键脚本，它会定位失败阶段并打印相关日志：

```bash
bash deploy.sh
```

也可以手工校验 Compose 配置：

```powershell
docker compose config
```

如果提示缺少环境变量，首次部署不要手工复制占位配置，直接让 `deploy.sh` 生成随机密码。已有数据卷时不要随意更换 MySQL 密码，更不要直接执行 `docker compose down -v`。完整排查步骤见 [Docker Compose 指南](./docker-compose.md)。
