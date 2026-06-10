# 代码生成器第三阶段总结

## 完成内容

- 新增回滚接口：`POST /system/code-generator/history/{id}/rollback`。
- 新增权限：`system:code-generator:rollback`。
- 回滚会删除本次生成记录中的生成文件，并清理生成模块目录。
- 回滚会删除生成菜单、按钮权限和对应角色授权。
- 回滚后历史状态更新为 `RolledBack`。
- 默认回滚不会删除业务表和业务数据。
- 回滚请求支持 `dropTable`，勾选后会在 MySQL 环境尝试删除业务表和表内数据。
- 已经 `RolledBack` 的记录可以继续执行“清理表”，只处理业务表，不重复删除文件和菜单。
- 前端生成记录列表和详情抽屉增加回滚入口，回滚弹窗提供危险删表选项。

## 安全边界

- 只允许回滚 `Success` 状态的生成记录。
- 已回滚记录再次回滚会返回错误。
- 文件删除仍走生成器安全路径校验，只删除生成器允许根目录内的文件。
- 业务表删除必须显式勾选，且表名必须是安全标识符。
- 非 MySQL 或非关系型数据库会跳过业务表删除，并返回跳过原因。
- 缺失文件不会阻止回滚，适合处理人工删除过部分文件的情况。

## 关键文件

- `src/MiniAdmin.Api/Program.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorAppService.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`
- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证结果

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj -c Release --filter "CodeGenerator"
```

结果：13 个生成器相关测试全部通过。
