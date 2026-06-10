# Redis 授权缓存任务执行文档

> 回补整理。

## 任务清单

- [x] 新增缓存配置
- [x] 接入 Redis 分布式缓存
- [x] 增加内存缓存 fallback
- [x] 实现用户授权缓存服务
- [x] 权限码读取优先走缓存
- [x] 权限变更清理缓存
- [x] 权限诊断展示缓存 key

## 涉及文件

### 后端

- `src/MiniAdmin.Infrastructure/Caching/CacheOptions.cs`
- `src/MiniAdmin.Infrastructure/Caching/DistributedUserAuthorizationCache.cs`
- `src/MiniAdmin.Application.Contracts/Caching/IUserAuthorizationCache.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfPermissionDiagnosticsRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfRoleRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

## 执行步骤

1. 增加 `Cache` 配置节，支持 `Redis` 和 `Memory`。
2. 注册 Redis 分布式缓存。
3. 未配置 Redis 时注册内存缓存。
4. 实现 `IUserAuthorizationCache`。
5. 鉴权读取权限码时先读缓存，未命中再查数据库。
6. 用户角色或角色菜单变更后清理相关缓存。
7. 权限诊断页面展示权限码缓存 key 和菜单缓存 key。

## 当前状态

已完成。

