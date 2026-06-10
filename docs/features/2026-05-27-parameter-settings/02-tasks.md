# 参数设置任务执行文档

> 回补整理。

## 任务清单

- [x] 定义系统参数实体
- [x] 新增参数 DTO 和保存请求
- [x] 实现参数分页和 CRUD
- [x] 前端参数设置页面
- [x] 种子菜单和权限码

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/SystemParameter.cs`
- `src/MiniAdmin.Application.Contracts/Parameters/SystemParameterDto.cs`
- `src/MiniAdmin.Application.Contracts/Parameters/SystemParameterListQuery.cs`
- `src/MiniAdmin.Application.Contracts/Parameters/SaveSystemParameterRequest.cs`
- `src/MiniAdmin.Application/Parameters/SystemParameterAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfSystemParameterRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/parameter.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/parameter/index.vue`

## 执行步骤

1. 创建系统参数实体。
2. 实现参数分页查询。
3. 实现参数创建、更新、删除。
4. API 增加 `/system/parameter` 系列接口。
5. 前端增加参数设置页面。
6. 增加菜单和按钮权限种子。

## 当前状态

已完成。

