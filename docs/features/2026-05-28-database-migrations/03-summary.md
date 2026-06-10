# 数据库迁移与初始化数据版本化总结文档

## 本次完成内容

本阶段把数据库结构管理从运行时兜底建表升级为 EF Core Migrations，并给初始化数据增加了版本记录。

- 新增首个迁移基线：`InitialCreate`
- 新增 seed 版本迁移：`AddDataSeedVersions`
- 新增表：`mini_data_seed_versions`
- 新增配置项：`Database:SchemaManagement`
- MySQL 默认走迁移体系。
- InMemory 仍走 `EnsureCreatedAsync()`。
- 已有旧 MySQL 库会被基线接管，不重复创建已有业务表。
- 基础菜单、权限、角色、用户、部门、岗位、字典、参数、公告改为按 seed version 执行。

## 配置说明

`Database:SchemaManagement` 支持以下值：

- `Auto`：默认值。关系型数据库使用 migrations，InMemory 使用 `EnsureCreatedAsync()`。
- `Migrations`：强制使用 migrations，适合 MySQL。
- `EnsureCreated`：使用旧的 `EnsureCreatedAsync()` 加兼容建表逻辑，适合临时调试。
- `None`：不管理数据库结构，只运行后续初始化逻辑。

推荐保持：

```json
{
  "Database": {
    "Provider": "MySql",
    "InitializeOnStartup": true,
    "SchemaManagement": "Auto"
  }
}
```

## 旧库接管逻辑

如果 MySQL 库里已经存在 `mini_users`、`mini_roles` 或 `mini_menus`，但没有 `__EFMigrationsHistory` 中的 `InitialCreate` 记录，启动时会：

1. 先执行旧的兼容补表/补列逻辑，尽量把旧库补到当前基线结构。
2. 创建 `__EFMigrationsHistory`。
3. 写入 `20260528021515_InitialCreate` 记录。
4. 再执行后续迁移，例如创建 `mini_data_seed_versions`。

这样旧库不会被 EF 当成空库重新建表。

## 初始化数据版本

当前基础数据版本：

```text
202605280001-baseline-system-data
```

它负责初始化：

- admin 角色
- 系统菜单和按钮权限
- 部门、岗位
- 字典、参数
- 通知公告
- admin/demo/auditor 初始化用户
- admin 用户角色和初始授权

同一个版本只会执行一次。后续新增菜单或权限时，应新增一个 seed version，而不是直接改旧版本并指望它重复执行。

## 后续新增表的标准流程

1. 修改实体和 `MiniAdminDbContext` 映射。
2. 生成迁移：

```powershell
dotnet ef migrations add <MigrationName> --project C:\monica\code\mini-admin\src\MiniAdmin.Infrastructure\MiniAdmin.Infrastructure.csproj --startup-project C:\monica\code\mini-admin\src\MiniAdmin.Api\MiniAdmin.Api.csproj --context MiniAdminDbContext --output-dir Persistence\Migrations
```

3. 如果需要初始化菜单、权限或字典，新增 seed version。
4. 跑完整测试。
5. 更新功能文档。

## 验证结果

- 迁移相关测试：3 个测试通过。
- 后端完整测试：69 个测试通过。
- 后端启动健康检查：`MiniAdmin.Api Healthy`。
