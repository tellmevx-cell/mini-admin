# Redis 快速兜底修复任务执行文档

## 任务清单

- [x] 排查后端、前端服务是否启动。
- [x] 验证前端代理和验证码接口是否可用。
- [x] 验证登录后用户信息、权限码、菜单链路是否超时。
- [x] 增加 Redis 默认短超时配置。
- [x] 增加 Redis 失败后的短期熔断，直接走内存缓存。
- [x] 增加 Redis 熔断测试。
- [x] 运行构建、测试并重启服务。

## 涉及文件

| 文件 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Infrastructure/Caching/ResilientDistributedCache.cs` | 修改 | Redis 失败后短期跳过主缓存，直接使用内存缓存 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs` | 修改 | Redis 连接字符串自动补充短超时参数 |
| `tests/MiniAdmin.Tests/ResilientDistributedCacheTests.cs` | 修改 | 增加失败后跳过 Redis 的回归测试 |

## 验证记录

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "ResilientDistributedCache"`：2 个测试通过。
- `dotnet build C:\monica\code\mini-admin\MiniAdmin.slnx`：构建通过。
- `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`：111 个测试通过。
- `http://localhost:5320/health`：后端返回 `Healthy`。
- `http://localhost:5666/`：前端返回 200。
- 登录、用户信息、权限码、菜单完整链路：约 3 秒内返回。
