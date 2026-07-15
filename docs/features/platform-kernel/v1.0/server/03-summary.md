# 平台内核 v1.0 交付总结

## 交付范围

- 新增 `MiniAdmin.Platform.Core` 与 `MiniAdmin.Platform.AspNetCore`，建立中立元数据和 ASP.NET Core 适配层。
- 应用服务通过 Dynamic API 暴露接口，Scalar 自动生成文档；标准 CRUD 不再需要 Controller 样板。
- PageRegistry 统一页面、路由、组件、权限和国际化，并在启动时同步数据库菜单。
- RBAC 与 ABAC 组合授权，授权、菜单、配置和字典接入版本化分布式缓存。
- 网关增加稳定哈希灰度、TraceId、限流和 Closed/Open/HalfOpen 熔断。
- 消息模板迁移 Scriban，增加短信、SignalR 实时通知和在线聊天。
- 开放平台支持 OAuth2/OIDC 与个人 AppKey/AppSecret HMAC 调用。
- 文件存储支持 Local、S3、OSS、COS、MinIO；系统监控补齐硬件、网络和运行时信息。

## 兼容与清理

- 保持已发布业务路由和菜单/权限 ID，避免现有角色授权失效。
- Customer、SampleOrder 已迁移到 Dynamic API 与 PageRegistry。
- 删除重复 Endpoint、MenuSeed 运行时扫描和 EF 迁移历史误生成模块。
- 文件流导入导出保留薄传输端点，避免把 HTTP 类型渗入应用服务。

## 验收

```powershell
dotnet build MiniAdmin.slnx -c Release
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj -c Release --no-restore
cd frontend/vue-vben-admin
pnpm -F @vben/web-antd exec vue-tsc --noEmit --skipLibCheck --pretty false
pnpm -F @vben/web-antd run build
cd ../../..
pnpm docs:build
```

后端测试共 `265` 项全部通过，前端类型检查、生产构建和文档站构建通过。Windows 验收机没有 Docker CLI；Compose 与镜像的运行验收由 Linux/1Panel 执行 `bash deploy.sh` 完成。
