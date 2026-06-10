# 项目运行管理构建历史与产物管理执行文档

## 后端

- `ProjectRuntimeServiceDto` 增加：
  - `buildArtifactPath`
  - `latestBuild`
  - `artifact`
- `SaveProjectRuntimeServiceRequest` 增加 `buildArtifactPath`。
- 新增 DTO：
  - `ProjectRuntimeBuildHistoryDto`
  - `ProjectRuntimeArtifactDto`
- 新增接口：
  - `GET /system/project-runtime/services/{serviceId}/build-history`
  - `GET /system/project-runtime/services/{serviceId}/artifact`
- 打包开始时写入历史记录，状态为 `Running`。
- 打包进程退出时更新历史记录，状态为 `Succeeded` 或 `Failed`。
- 读取 overview 时计算最新构建记录和产物信息。

## 前端

- 服务行展示最近构建状态。
- 日志控制台下方或旁侧展示：
  - 最近构建状态、耗时、结束时间、ExitCode。
  - 产物路径、是否存在、大小、更新时间。
  - “打开产物”按钮。
- 打包完成后自动刷新构建历史和产物信息。

## 验证

- 单元测试覆盖构建历史读写。
- 单元测试覆盖产物信息计算。
- 后端 `dotnet build` 和项目运行管理测试通过。
- 前端 `pnpm run build:antd` 通过。
