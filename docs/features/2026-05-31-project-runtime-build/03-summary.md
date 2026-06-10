# 项目运行管理打包能力总结

## 完成内容

- 日志控制台改为固定高度视窗，日志内容在控制台内部滚动。
- 实时输出开启时，刷新日志后会自动滚动到最新输出。
- 打包日志实时输出时会静默刷新服务概览，确保 `ExitCode=0` 后状态从“打包中”更新为“打包成功”。
- 新增运行日志/打包日志切换。
- 服务行新增“打包”按钮。
- 后端新增服务级打包接口：
  - `POST /system/project-runtime/services/{serviceId}/build`
  - `GET /system/project-runtime/services/{serviceId}/build-logs`
- 后端为服务增加构建配置和构建状态：
  - `buildCommand`
  - `buildArguments`
  - `buildWorkingDirectory`
  - `buildLogFileName`
  - `buildLogPath`
  - `buildState`
- 运行进程和打包进程分开管理，打包不会改变服务运行状态。
- 默认支持：
  - 当前 MiniAdmin API：`dotnet publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api`
  - Vben Web：`pnpm run build:antd`
  - 普通 .NET 服务：`dotnet publish -c Release`
  - 普通 Vue/React：`pnpm run build`
  - uniapp：`pnpm run build:h5`

## 验证结果

- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`：通过，0 警告，0 错误。
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter ProjectRuntimeAppServiceTests`：通过，2 个测试。
- `pnpm run build:antd`：通过，`11 successful`；末尾仍有当前环境已有的 Node `v22.22.0` 提示。
- `npx impeccable --json ...project-runtime/index.vue`：通过，返回 `[]`。
- 修复“打包日志已显示 ExitCode=0，但服务状态仍卡在打包中”的前端状态同步问题，并重新执行 `pnpm run build:antd`：通过。
- 后端 `http://localhost:5320/health`：Healthy。
- 前端 `http://localhost:5666/`：200。

## 使用方式

进入“开发工具 / 项目运行管理”，选择服务后：

- 点击“打包”执行该服务的构建命令。
- 日志控制台会自动切换到“打包日志”。
- “实时输出”开启时会自动滚动到最新行。
- 如果需要查看历史日志，先关闭“实时输出”，再手动滚动。

## 后续计划

- 项目登记向导中增加项目类型选择：.NET、Vue、React、uniapp、自定义。
- 支持编辑构建命令和构建日志路径。
- 支持构建产物目录展示。
- 支持全项目流水线：后端打包、前端打包、产物归档、部署。
