# MiniAdmin

MiniAdmin 是一个面向二次开发的企业级后台管理系统。它把 SaaS 多租户、RBAC 权限、工作流审批、消息中心、审计日志、代码生成器、系统监控、YARP 网关和文档站整合在同一个开箱即用的工程里，适合作为中后台、内部运营平台、低代码业务平台或多租户管理系统的基础模板。

[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![CI](https://github.com/tellmevx-cell/mini-admin/actions/workflows/ci.yml/badge.svg)](https://github.com/tellmevx-cell/mini-admin/actions/workflows/ci.yml)
[![Backend](https://img.shields.io/badge/.NET-10-blue.svg)](src/MiniAdmin.Api)
[![Frontend](https://img.shields.io/badge/Vue-Vben%20Admin-42b883.svg)](frontend/vue-vben-admin)
[![Docs](https://img.shields.io/badge/Docs-VitePress-646cff.svg)](docs-site)

## 为什么选择 MiniAdmin

- **可直接二开**：后端按 `Domain / Application / Infrastructure / Api` 分层，前端基于 Vben Admin，菜单、权限、业务模块都有清晰扩展入口。
- **真实后台能力**：内置用户、角色、菜单、部门、岗位、字典、参数、文件、通知、审计、登录安全、在线用户和系统监控。
- **工作流优先**：支持流程定义、审批中心、条件分支、抄送节点、审批记录、消息提醒和业务绑定。
- **SaaS 多租户**：支持平台租户、租户套餐、租户初始化模板、租户管理员开通和租户数据隔离。
- **代码生成器**：支持从表结构生成可运行 CRUD，并沉淀生成历史、产物治理、回滚和工作流绑定能力。
- **工程化完整**：包含测试、文档站、运行管理、定时任务、事件总线、工作单元和本地/生产配置说明。
- **网关可演进**：内置 `MiniAdmin.Gateway`，基于 YARP 提供统一 `/api` 入口、健康检查和入口限流，为后续微服务拆分预留边界。

## 技术栈

| 模块 | 技术 |
| --- | --- |
| 后端 | .NET 10, ASP.NET Core Minimal API, EF Core |
| 网关 | YARP Reverse Proxy, ASP.NET Core RateLimiter |
| 数据库 | 默认 InMemory，支持 MySQL |
| 缓存 | Memory Cache，支持 Redis 并具备失败兜底 |
| 前端 | Vue 3, Vben Admin, Ant Design Vue, Pinia, Vite |
| 文档 | VitePress |
| 测试 | xUnit, WebApplicationFactory |

## 功能概览

### 系统基础

- JWT 登录、验证码、登录失败锁定、密码策略和在线会话管理。
- 用户、角色、菜单、权限码、部门、岗位、字典、系统参数和公告管理。
- RBAC 权限控制、数据权限、权限诊断链路和缓存刷新。
- 审计日志、实体变更追踪、安全事件和操作日志。

### SaaS 租户

- 平台租户管理、租户启停、到期控制和登录租户选项。
- 租户套餐与菜单授权。
- 租户管理员自动开通。
- 标准企业模板初始化部门、岗位、角色和基础权限。
- 租户内角色、部门、岗位编码唯一，避免多租户基础数据冲突。

### 工作流与消息中心

- 流程定义、流程实例、审批任务、我的待办、我的申请、我的抄送和我的已办。
- 审批节点、条件节点、抄送节点、结束节点和可视化流程画布。
- 审批附件、评论、催办、撤回、版本发布和业务表单绑定。
- 消息通知中心、已读/未读追踪、模板中心、通知策略和投递重试。

### 运维与平台能力

- 文件上传、下载、异常文件标记和存储一致性检查。
- 定时任务、任务日志、系统监控看板和告警中心。
- 项目运行管理，可在管理端查看服务、日志、构建和产物。
- 本地事件总线和工作单元，便于扩展领域事件和事务边界。
- MiniAdmin.Gateway 网关，支持 `/api` 统一代理、网关健康检查和入口限流。

### 代码生成与二开

- 表结构读取、字段选择、查询条件、控件类型和字典绑定。
- 生成前预览、生成后安装、生成历史、产物治理和回滚。
- 支持生成业务模块，并可绑定工作流审批。

## 功能截图展示

功能展示图已沉淀到文档站，适合 GitHub 访客快速了解系统界面和能力边界：

- [功能截图展示](docs-site/features/showcase.md)
- [功能总览](docs-site/features/overview.md)
- [工作流与消息中心运行手册](docs-site/runbooks/workflow-message-center.md)

如果你在本地启动了前后端，可以重新生成截图：

```powershell
pnpm screenshots:features
```

默认访问 `http://localhost:5666`，如果你的本地前端端口不同，可以指定：

```powershell
$env:MINIADMIN_WEB_URL = "http://localhost:5600"
pnpm screenshots:features
```

## 快速开始

### 环境要求

- .NET SDK 10+
- Node.js 22.18+ 或 24+
- pnpm 11+
- MySQL 可选，默认可以直接使用 InMemory 启动
- Redis 可选，默认使用 Memory Cache

### 启动后端

```powershell
dotnet restore
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

健康检查：

```powershell
Invoke-WebRequest -Uri http://localhost:5021/health -UseBasicParsing
```

### 启动前端

```powershell
cd frontend/vue-vben-admin
pnpm install
pnpm run dev:antd
```

访问：

```text
http://localhost:5666
```

默认账号：

| 场景 | 租户编码 | 用户名 | 密码 |
| --- | --- | --- | --- |
| 平台管理员 | 留空 | `admin` | `123456` |
| 演示租户 | `demo` | `demo` | `123456` |

> 首次公开部署前请务必修改默认密码和 `Jwt:SigningKey`。

### Docker Compose 一键体验

如果你希望同时启动 MySQL、Redis、后端 API、YARP 网关和前端静态站点，可以使用 Docker Compose：

Linux / 1Panel 服务器推荐直接执行：

```bash
bash scripts/deploy-mini-admin.sh
```

本地手动体验也可以执行：

```powershell
Copy-Item .env.example .env
# 编辑 .env，替换 JWT、MySQL、Redis 相关密码
docker compose up -d --build
```

访问：

```text
前端：http://localhost:5666
网关：http://localhost:8088/health
API 代理：http://localhost:8088/api/health
后端直连：http://localhost:8080/health
```

完整说明见 [Docker Compose 指南](docs-site/guide/docker-compose.md)。

### 可选启动网关

本地开发时可以在 API 旁边启动网关：

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
dotnet run --project src/MiniAdmin.Gateway/MiniAdmin.Gateway.csproj --urls http://localhost:8088
```

网关访问：

```text
http://localhost:8088/api/health
```

详细说明见 [网关与微服务演进](docs-site/guide/gateway-microservices.md)。

## 使用 MySQL / Redis

仓库默认配置不包含任何真实密钥。开发环境推荐通过环境变量或本地忽略文件配置连接信息。

PowerShell 示例：

```powershell
$env:Database__Provider = "MySql"
$env:ConnectionStrings__MiniAdmin = "Server=127.0.0.1;Port=3306;Database=mini_admin;User=root;Password=your_password;CharSet=utf8mb4;SslMode=None;AllowPublicKeyRetrieval=True;"
$env:Cache__Provider = "Redis"
$env:Cache__Redis__Configuration = "127.0.0.1:6379,password=your_password,abortConnect=False,defaultDatabase=10"

dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

也可以参考 [appsettings.Development.example.json](src/MiniAdmin.Api/appsettings.Development.example.json) 创建你自己的 `src/MiniAdmin.Api/appsettings.Development.json`。该文件已被 `.gitignore` 忽略，不应提交到仓库。

## 文档站

```powershell
pnpm install
pnpm docs:dev
```

构建静态文档：

```powershell
pnpm docs:build
```

文档源码位于 [docs-site](docs-site)，需求、任务和实现记录位于 [docs/features](docs/features) 与 [docs/runbooks](docs/runbooks)。

## 后端二开指南

- 新增领域对象优先放到 [src/MiniAdmin.Domain](src/MiniAdmin.Domain)。
- 对外契约放到 [src/MiniAdmin.Application.Contracts](src/MiniAdmin.Application.Contracts)。
- 应用服务放到 [src/MiniAdmin.Application](src/MiniAdmin.Application)。
- EF 仓储、缓存、文件、通知、定时任务等基础设施放到 [src/MiniAdmin.Infrastructure](src/MiniAdmin.Infrastructure)。
- API 端点注册放到 [src/MiniAdmin.Api](src/MiniAdmin.Api)。
- 新增菜单和权限时同步补种子数据、权限码和前端页面。
- 涉及事务边界和领域事件时优先使用 `IUnitOfWork` 与 `ILocalEventBus`。

## 前端二开指南

- 主应用在 [frontend/vue-vben-admin/apps/web-antd](frontend/vue-vben-admin/apps/web-antd)。
- 业务接口放到 `src/api`。
- 页面放到 `src/views`。
- 后端菜单会动态驱动前端路由，新增页面时注意组件路径与菜单 `component` 保持一致。
- 保留 Vben Admin 的工程结构，避免把业务代码散落到基础包里。

## 测试

运行后端测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
```

常用定向验证：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter PlatformInfrastructureTests
```

前端类型检查：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd run typecheck
```

## 开源发布前检查

- 不提交 `appsettings.Development.json`、`.env.local`、日志、构建产物和本地上传文件。
- 不提交真实数据库、Redis、MinIO、SMTP、Webhook、JWT 生产密钥。
- 生产环境必须替换默认账号密码、JWT 密钥和对象存储配置。
- 如果你修改了 Vben Admin 子项目，请同时遵守其目录内的 [LICENSE](frontend/vue-vben-admin/LICENSE)。

## 贡献

欢迎提交 Issue、Pull Request、功能建议和二开实践反馈。开始前建议阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 与 [SECURITY.md](SECURITY.md)。

建议贡献前先运行相关测试，并在 PR 中说明：

- 改动目的和影响范围。
- 是否包含数据库结构、权限码或菜单种子变更。
- 如何验证。

## 许可证

MiniAdmin 使用 [MIT License](LICENSE) 开源。你可以自由使用、复制、修改、合并、发布、分发、再授权和商用，但需要保留版权与许可声明。

本仓库包含的第三方开源项目、依赖和资源仍遵循其各自许可证。
