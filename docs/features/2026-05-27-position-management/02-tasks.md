# 岗位管理任务执行文档

> 回补整理。

## 任务清单

- [x] 定义岗位实体
- [x] 新增岗位 DTO 和查询请求
- [x] 实现岗位分页查询和 CRUD
- [x] 前端岗位管理页面
- [x] 用户表单接入岗位选择
- [x] 用户列表接入岗位过滤

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Position.cs`
- `src/MiniAdmin.Application.Contracts/Positions/PositionDto.cs`
- `src/MiniAdmin.Application.Contracts/Positions/PositionListQuery.cs`
- `src/MiniAdmin.Application.Contracts/Positions/SavePositionRequest.cs`
- `src/MiniAdmin.Application/Positions/PositionAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfPositionRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/position.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/position/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

## 执行步骤

1. 增加岗位实体和 EF 配置。
2. 实现岗位分页、创建、更新、删除。
3. 在系统初始化中加入基础岗位。
4. 前端岗位管理页面使用表格和弹窗维护。
5. 用户表单加载岗位选项。
6. 用户列表查询带上 `positionId`。

## 当前状态

已完成。

