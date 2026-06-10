# 项目运行管理打包能力执行文档

## 数据模型

- `ProjectRuntimeServiceDto` 增加构建配置：
  - `buildCommand`
  - `buildArguments`
  - `buildWorkingDirectory`
  - `buildLogFileName`
  - `buildLogPath`
  - `buildState`
- `SaveProjectRuntimeServiceRequest` 同步增加这些字段，便于后续可视化编辑。

## 后端接口

- `POST /system/project-runtime/services/{serviceId}/build`
  - 启动一次服务级打包进程。
  - 如果该服务已有打包进程在运行，返回运行中状态，不重复启动。
- `GET /system/project-runtime/services/{serviceId}/build-logs`
  - 读取该服务构建日志最后若干行。

## 默认命令推断

创建默认配置或登记项目时，根据项目文件推断打包命令：

- 发现 `MiniAdmin.slnx`：API 服务使用 `dotnet publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api`
- 发现 Vben 项目：Web 服务使用 `pnpm run build:antd`
- 普通 Vue/React/uniapp 项目第一版通过服务配置保留扩展点，后续做项目登记向导时可暴露模板选择。

## 前端交互

- 服务行增加“打包”按钮。
- 日志控制台增加“运行日志/打包日志”切换。
- 点击打包后：
  - 调用打包接口。
  - 切换到打包日志。
  - 打开实时输出。
  - 刷新服务概览和日志。
- 日志控制台固定高度，内部滚动。
- 实时输出开启时自动滚动到底部；关闭时保持用户当前滚动位置。

## 测试点

- 后端可读取独立构建日志。
- 默认配置包含 API 和 Vben 的构建命令。
- 前端构建通过。
- 后端构建和项目运行管理测试通过。
