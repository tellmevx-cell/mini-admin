# 项目运行管理构建历史与产物管理总结

## 完成内容

- 新增构建历史本地持久化：
  - 文件位置：`data/project-runtime/build-history.json`
  - 打包开始写入 `Running` 记录。
  - 打包进程退出后更新为 `Succeeded` 或 `Failed`。
  - 记录开始时间、结束时间、耗时、退出码、命令、工作目录、日志路径和产物路径。
- 新增构建产物信息：
  - 服务配置支持 `buildArtifactPath`。
  - overview 返回 `latestBuild` 和 `artifact`。
  - 支持判断产物是否存在、类型、大小和最后更新时间。
- 新增后端接口：
  - `GET /system/project-runtime/services/{serviceId}/build-history`
  - `GET /system/project-runtime/services/{serviceId}/artifact`
  - `POST /system/project-runtime/services/{serviceId}/artifact/open`
- 前端页面增强：
  - 服务行展示最近打包状态和产物状态。
  - 日志控制台展示最近构建、耗时、ExitCode、产物路径和大小。
  - 支持打开本地产物路径。
  - 支持刷新构建/产物状态。

## 默认产物路径

- MiniAdmin API：`artifacts/publish/mini-admin-api`
- Vben Web：`frontend/vue-vben-admin/apps/web-antd/dist.zip`
- 普通前端项目：`dist`
- 普通 .NET 项目：`bin/Release`

## 验证结果

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter ProjectRuntimeAppServiceTests`：通过，4 个测试。
- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`：通过，0 警告，0 错误。
- `pnpm run build:antd`：通过，`11 successful`；末尾仍有当前环境已有的 Node `v22.22.0` 提示。
- `npx impeccable --json ...project-runtime/index.vue`：通过，返回 `[]`。
- 后端 `http://localhost:5320/health`：Healthy。
- 前端 `http://localhost:5666/`：200。

## 下一步

- 增加服务/构建配置可视化编辑。
- 增加构建历史列表弹窗。
- 在工作流模块中复用打包、构建历史和产物信息。
