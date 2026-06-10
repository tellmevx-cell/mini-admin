# JWT 登录认证任务执行文档

## 任务清单

- [x] 定义登录请求和响应 DTO
- [x] 实现密码校验
- [x] 实现 JWT 生成
- [x] 注册 JwtBearer 认证
- [x] 保护后端接口
- [x] 对接 Vben 登录

## 涉及文件

- `src/MiniAdmin.Application.Contracts/Auth/*`
- `src/MiniAdmin.Application/Auth/AuthAppService.cs`
- `src/MiniAdmin.Infrastructure/Auth/JwtTokenService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfAuthRepository.cs`
- `src/MiniAdmin.Api/Program.cs`
- `docs/superpowers/plans/2026-05-26-jwt-authentication.md`

## 执行步骤

1. 定义认证契约。
2. 实现用户查询和密码校验。
3. 生成 JWT。
4. 注册 Bearer 认证。
5. 前端登录后携带 token 调接口。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "Login|Protected|AccessCodes"
```

## 当前状态

已完成，本文档为回补整理。
