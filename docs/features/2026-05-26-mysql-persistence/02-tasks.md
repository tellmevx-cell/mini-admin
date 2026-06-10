# MySQL 持久化任务执行文档

## 任务清单

- [x] 增加数据库配置项
- [x] 接入 MySQL Provider
- [x] 保留 InMemory 测试能力
- [x] 创建 DbContext
- [x] 编写数据库初始化器
- [x] 初始化基础数据

## 涉及文件

- `src/MiniAdmin.Api/appsettings.json`
- `src/MiniAdmin.Api/appsettings.Development.json`
- `src/MiniAdmin.Infrastructure/Persistence/DatabaseOptions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `docs/03-mysql-persistence.md`

## 执行步骤

1. 添加数据库配置读取。
2. 根据 Provider 选择 MySQL 或 InMemory。
3. 建立实体映射。
4. 启动时初始化数据库和种子数据。
5. 测试环境通过环境变量使用 InMemory。

## 验证命令

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminTest'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

## 当前状态

已完成，本文档为回补整理。
