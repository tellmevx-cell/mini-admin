# 登录安全与账号锁定任务执行文档

## 任务清单

- [x] 设计登录失败状态
- [x] 增加验证码接口
- [x] 登录失败后要求验证码
- [x] 增加锁定逻辑
- [x] 增加管理员解锁接口
- [x] 前端登录页显示验证码输入
- [x] 用户列表显示锁定状态和解锁按钮

## 涉及文件

- `src/MiniAdmin.Application.Contracts/Auth/*`
- `src/MiniAdmin.Infrastructure/Auth/DistributedLoginSecurityService.cs`
- `src/MiniAdmin.Infrastructure/Caching/CacheOptions.cs`
- `src/MiniAdmin.Api/Program.cs`
- `frontend/vue-vben-admin/apps/web-antd/src/views/_core/authentication/login.vue`
- `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`
- `docs/superpowers/specs/2026-05-27-login-security-design.md`
- `docs/superpowers/plans/2026-05-27-login-security.md`

## 执行步骤

1. 增加登录安全配置。
2. 实现分布式缓存计数。
3. 增加验证码接口。
4. 登录失败后返回是否需要验证码。
5. 锁定账号并返回剩余时间。
6. 用户列表和管理员解锁按钮对接。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "LoginSecurity|Unlock"
```

## 当前状态

已完成，本文档为回补整理。
