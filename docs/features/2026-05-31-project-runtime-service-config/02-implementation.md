# 项目运行管理服务配置执行文档

## 前端

- 服务区增加“新增服务”按钮。
- 每个服务行增加“配置”和“删除”按钮。
- 新增/编辑服务共用一个弹窗。
- 弹窗字段：
  - 服务名称、服务类型、排序、启用
  - 运行命令、运行参数、工作目录
  - 端口、健康检查、访问地址、运行日志
  - 打包命令、打包参数、打包工作目录
  - 打包日志、产物路径
- 保存时将当前项目转换成 `SaveProjectRuntimeProjectRequest`，更新对应工作区的服务列表后调用项目更新接口。

## 模板

- `.NET API`：`dotnet run` / `dotnet publish -c Release`
- `Vue/Vben`：`pnpm run dev` / `pnpm run build`
- `React`：`pnpm run dev` / `pnpm run build`
- `uniapp`：`pnpm run dev:h5` / `pnpm run build:h5`
- `自定义`：空命令，用户自行填写。

## 验证

- 前端构建通过。
- 后端健康检查正常。
- 页面可新增、编辑、删除服务配置。
