# 项目运行管理打包能力设计

## 目标

项目运行管理从“启动/停止/看日志”扩展到“启动/停止/打包/看运行日志/看打包日志”。第一版聚焦服务级打包，不做完整发布流水线。

## 设计

每个服务拥有一组构建配置：构建命令、参数、工作目录、日志文件名和可选日志路径。后端执行打包时启动一次性进程，将标准输出和错误输出写入独立构建日志。运行进程状态和构建进程状态分开保存，避免打包影响服务运行状态。

前端仍以服务为操作单位。服务行提供“打包”按钮，日志控制台提供“运行日志/打包日志”切换。点击打包后自动切到打包日志并开启实时输出，用户可以看到构建过程持续追加。

## 默认命令

- .NET：`dotnet publish src/MiniAdmin.Api/MiniAdmin.Api.csproj -c Release -o artifacts/publish/mini-admin-api`
- Vben/Vue：`pnpm run build:antd`
- React：`pnpm run build`
- uniapp：`pnpm run build:h5`

## 风险控制

- 第一版只执行本地配置命令，不做远程部署。
- 如果构建进程正在运行，重复点击只返回当前构建状态，不再次启动。
- 日志路径仍限制在工作区内，避免读取任意文件。

## 验证

- 单元测试覆盖构建日志读取。
- `dotnet build` 验证后端编译。
- `dotnet test --filter ProjectRuntimeAppServiceTests` 验证运行管理测试。
- `pnpm run build:antd` 验证前端编译。
