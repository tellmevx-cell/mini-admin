# 本地开发

本页说明日常开发时如何启动、验证和排查 MiniAdmin。

## 推荐工作流

1. 从主分支创建功能分支。
2. 阅读或新增 `docs/features/YYYY-MM-DD-feature-name` 功能文档。
3. 后端先补 DTO、服务、接口和测试。
4. 前端补 API、页面、菜单权限和交互。
5. 跑目标测试、类型检查和构建。
6. 更新功能总结或运行手册。

## 后端开发命令

启动 API：

```powershell
dotnet run --project src/MiniAdmin.Api/MiniAdmin.Api.csproj --urls http://localhost:5021
```

运行全部测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj
```

运行指定测试：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter WorkflowAppServiceTests
```

## 前端开发命令

```powershell
cd frontend/vue-vben-admin
pnpm install
pnpm run dev:antd
```

类型检查：

```powershell
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

生产构建：

```powershell
pnpm -F @vben/web-antd build
```

## 文档开发命令

```powershell
pnpm --dir docs-site install
pnpm docs:dev
pnpm docs:build
pnpm docs:preview
```

## 推荐验证组合

平台层变更建议至少运行：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter "WorkflowAppServiceTests|NotificationTemplateAppServiceTests|NotificationPolicyAppServiceTests|NotificationDeliveryServiceTests" --no-restore
```

前端变更建议至少运行：

```powershell
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
```

文档站变更建议运行：

```powershell
pnpm docs:build
```

## 本地数据模式

MiniAdmin 支持快速开发和正式数据库两种思路：

| 模式 | 适用场景 |
| --- | --- |
| InMemory | 快速体验、功能验证、无外部依赖测试 |
| MySQL | 联调、准生产、生产环境 |

如果要验证迁移、索引、唯一约束和租户隔离，请使用 MySQL。只验证页面和流程链路时，InMemory 更轻。

## 不要提交的内容

以下内容一般不应提交：

- `bin/`、`obj/`
- `node_modules/`
- `.vitepress/dist/`
- 本地日志 `*.log`
- 本地密钥和 `appsettings.*.local.json`
- 临时上传文件和本地运行数据

根目录 `.gitignore` 已经覆盖大部分本地运行产物。
