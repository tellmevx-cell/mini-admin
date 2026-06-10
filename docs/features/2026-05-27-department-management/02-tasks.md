# 部门管理任务执行文档

> 回补整理。

## 任务清单

- [x] 定义部门实体
- [x] 新增部门 DTO 和保存请求
- [x] 实现部门树查询
- [x] 实现部门新增、编辑、删除
- [x] 前端部门管理页面
- [x] 用户管理复用部门树
- [x] 数据权限接入部门关系

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/Department.cs`
- `src/MiniAdmin.Application.Contracts/Departments/DepartmentItemDto.cs`
- `src/MiniAdmin.Application.Contracts/Departments/SaveDepartmentRequest.cs`
- `src/MiniAdmin.Application/Departments/DepartmentAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfDepartmentRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/department.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/department/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

## 执行步骤

1. 创建 `Department` 实体并配置父子关系。
2. 新增部门保存请求和树节点 DTO。
3. Repository 查询部门后组装树结构。
4. API 暴露部门列表、新增、编辑、删除接口。
5. 前端部门页面使用树表维护部门。
6. 用户页面加载部门树并传入用户查询条件。

## 当前状态

已完成。

