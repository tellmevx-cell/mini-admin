# 密码修改与重置任务执行文档

> 回补整理。

## 任务清单

- [x] 新增修改当前用户密码请求
- [x] 新增管理员重置密码请求
- [x] 实现密码策略校验
- [x] 实现旧密码校验
- [x] 密码变更后更新用户安全版本
- [x] 前端用户页面增加重置密码按钮
- [x] 接入按钮权限

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/Users/ChangeCurrentUserPasswordRequest.cs`
- `src/MiniAdmin.Application.Contracts/Users/ResetUserPasswordRequest.cs`
- `src/MiniAdmin.Application.Contracts/Users/PasswordOperationResult.cs`
- `src/MiniAdmin.Application/Users/UserAppService.cs`
- `src/MiniAdmin.Infrastructure/Auth/PasswordService.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 新增当前用户修改密码接口。
2. 修改密码时校验旧密码。
3. 新增管理员重置用户密码接口。
4. 增加密码确认和密码策略校验。
5. 密码变更成功后更新用户安全版本。
6. 前端用户管理增加重置密码按钮。
7. 按钮和接口都要求 `system:user:reset-password`。

## 当前状态

已完成。

