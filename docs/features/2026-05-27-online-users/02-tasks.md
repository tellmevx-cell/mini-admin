# 在线用户与强制下线任务执行文档

> 回补整理。

## 任务清单

- [x] 定义在线用户实体
- [x] 定义登录日志实体和查询
- [x] 登录成功写入在线用户和登录日志
- [x] 实现在线用户列表
- [x] 实现强制下线接口
- [x] token 校验接入用户安全版本
- [x] 前端处理 401 并跳转登录页

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/OnlineUser.cs`
- `src/MiniAdmin.Domain/Entities/LoginLog.cs`
- `src/MiniAdmin.Application.Contracts/OnlineUsers/OnlineUserDto.cs`
- `src/MiniAdmin.Application.Contracts/OnlineUsers/LoginLogDto.cs`
- `src/MiniAdmin.Application/OnlineUsers/OnlineUserAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfOnlineUserRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/online-user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/login-log.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/online-user/index.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/login-log/index.vue`

## 执行步骤

1. 登录成功后记录登录日志。
2. 登录成功后写入在线用户记录。
3. 在线用户页面分页查询在线记录。
4. 强制下线时更新用户安全版本或 token 版本。
5. token 校验时比较版本，旧 token 直接拒绝。
6. 前端请求拦截器遇到登录失效时清理缓存并跳转登录。

## 当前状态

已完成。

