# 项目运行管理第一版设计

## 设计目标

第一版建设一个本地项目运行控制台，支持多项目、多工作区、多服务。它服务当前 MiniAdmin + Vben 的开发流程，同时保留管理其他本地项目的能力。

## 核心模型

- Project：项目级信息，包含名称、编码、根目录、仓库地址和排序。
- Workspace：项目下的本地工作区，包含名称、路径、分支、标签和启用状态。
- Service：工作区下的可运行服务，包含类型、命令、参数、工作目录、端口、健康地址、访问地址和日志文件。
- RuntimeState：运行态，不持久化到数据库，记录由本面板启动的 PID、启动时间、退出码和最后错误。

## 存储

配置先写入 `data/project-runtime/projects.json`。这样可以避免第一版引入迁移和租户边界问题，也符合“本地开发控制台”的定位。后续如果要做团队共享，可以把配置迁移到数据库或集中配置中心。

## 后端接口

- `GET /system/project-runtime/overview`：返回所有项目、工作区、服务和运行状态。
- `POST /system/project-runtime/projects`：新增项目。
- `PUT /system/project-runtime/projects/{projectId}`：更新项目。
- `DELETE /system/project-runtime/projects/{projectId}`：删除项目。
- `POST /system/project-runtime/workspaces/{workspaceId}/start`：启动工作区内所有启用服务。
- `POST /system/project-runtime/workspaces/{workspaceId}/stop`：停止工作区内所有服务。
- `POST /system/project-runtime/services/{serviceId}/start`：启动服务。
- `POST /system/project-runtime/services/{serviceId}/stop`：停止服务。
- `POST /system/project-runtime/services/{serviceId}/restart`：重启服务。
- `GET /system/project-runtime/services/{serviceId}/logs`：读取最近日志。

## 前端页面

页面沿用系统监控看板的风格。左侧是项目列表，中间是工作区和服务卡片，右侧是日志与运行信息。第一版提供新增项目弹窗，复杂工作区和服务编辑先放到项目表单的简化 JSON/字段组合里，后续再升级成完整向导。

## 安全边界

该功能只面向管理员。后端只启动已保存的服务定义，不提供任意终端输入接口。工作目录必须存在，日志路径由系统生成。停止操作只停止由本面板启动并记录的 PID，避免误杀用户手动启动的进程。

## 错误处理

- 工作目录不存在：返回 400。
- 命令为空：返回 400。
- 服务已由本面板启动：返回当前状态。
- 端口已占用但不是本面板 PID：标记 Unknown，并提示端口已被占用。
- 进程退出：状态变为 Failed 或 Stopped，并保留退出码和日志。

