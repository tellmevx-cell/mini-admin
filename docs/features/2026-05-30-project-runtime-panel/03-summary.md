# 项目运行管理第一版总结

## 完成内容

- 新增“开发工具 / 项目运行管理”菜单和权限：
  - `system:project-runtime:query`
  - `system:project-runtime:manage`
  - `system:project-runtime:log`
- 后端新增项目运行管理合同层、JSON 配置存储、进程启动停止、端口/健康探测和日志读取。
- 默认配置采用 Project -> Workspace -> Service 模型，并内置当前 MiniAdmin 的 API 与 Vben Web 服务。
- 新增项目运行管理 API：
  - 项目总览
  - 项目新增、更新、删除
  - 工作区启动、停止
  - 服务启动、停止、重启
  - 服务日志读取
- 前端新增项目运行管理页面：
  - 左侧项目列表
  - 中间工作区和服务卡片
  - 右侧运行详情和最近日志
  - 支持登记本地项目目录
  - 支持服务启动、停止、重启、打开访问地址、刷新日志
- 已补充实时日志体验：
  - 服务配置支持 `logPath`，默认 MiniAdmin API 读取 `backend-dev.log`。
  - 默认 Vben Web 读取 `frontend-dev.log`。
  - 日志面板每 2 秒自动刷新，可手动关闭“实时”开关。
- 已按设计审查结果完成页面重构：
  - 从三列卡片式看板调整为“运行控制台”布局。
  - 顶部保留紧凑概览，左侧聚焦项目和工作区选择。
  - 中间区域聚焦当前上下文、工作区操作和服务列表。
  - 底部升级为大尺寸日志控制台，避免实时输出成为次要信息。
  - “实时输出”开关放入日志工具栏，开关状态更直观。

## 设计审查记录

- `$critique` 自动扫描：`npx impeccable --json ...project-runtime/index.vue` 返回 `[]`，未发现确定性反模式。
- 人工审查发现第一版主要问题：
  - 日志窗口过小，不符合“项目运行管理”的核心使用场景。
  - 项目、工作区、服务、日志四块区域权重过于平均，用户视线没有明确落点。
  - 实时输出开关存在，但位置和视觉权重偏弱。
- 本次优化方向：
  - 降低装饰性卡片密度，改为更接近控制台的简洁布局。
  - 把服务列表做成横向信息行，便于扫描状态、端口和健康检查。
  - 把日志区域做大，并保留刷新、清屏、实时输出开关。

## 验证结果

- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`：通过，0 警告，0 错误。
- `pnpm run build:antd`：通过，生成 `project-runtime` 页面产物；末尾仍有当前环境已有的 Node 版本提示。
- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter ProjectRuntimeAppServiceTests`：通过。
- 页面重构后再次执行 `npx impeccable --json C:\monica\code\mini-admin\frontend\vue-vben-admin\apps\web-antd\src\views\system\project-runtime\index.vue`：通过，返回 `[]`。
- 页面重构后再次执行 `pnpm run build:antd`：通过；末尾仍有当前环境已有的 Node 版本提示。
- 后端已启动，健康检查 `http://localhost:5320/health` 返回 Healthy。
- 前端 `http://localhost:5666/` 返回 200。
- 浏览器自动化冒烟验证被当前沙盒阻止，未继续绕行。

## 后续计划

- 第二版补完整工作区/服务编辑向导，避免手改 JSON。
- 支持创建和删除 git worktree。
- 支持从远程仓库克隆项目。
- 支持环境变量配置档案。
- 支持端口冲突一键定位和建议处理。
- 支持更多进程健康策略和历史运行记录。
