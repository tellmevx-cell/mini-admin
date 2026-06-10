# 代码生成器一期任务执行文档

## 任务清单

- [x] 编写后端失败测试：预览生成文件和权限。
- [x] 编写后端失败测试：冲突文件默认阻止写入。
- [x] 编写后端失败测试：生成历史落库。
- [x] 新增代码生成器契约 DTO、Request、Query。
- [x] 新增代码生成器实体和 DbContext 配置。
- [x] 新增 MySQL `information_schema` 元数据读取能力。
- [x] 新增模板渲染服务。
- [x] 新增文件写入白名单校验。
- [x] 新增预览服务。
- [x] 新增生成服务。
- [x] 新增生成历史记录。
- [x] 新增 Minimal API endpoints 和权限保护。
- [x] 新增菜单和权限种子。
- [x] 新增前端 API 文件。
- [x] 新增代码生成器页面。
- [x] 运行后端 CodeGenerator 过滤测试。
- [x] 运行后端全量测试。
- [x] 运行前端构建。
- [x] 编写 `03-summary.md`。

## 涉及文件

### 后端

- `src/MiniAdmin.Domain/Entities/CodeGenerationHistory.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorAppService.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-05-29-code-generator-basic/01-requirements.md`
- `docs/features/2026-05-29-code-generator-basic/02-tasks.md`
- `docs/features/2026-05-29-code-generator-basic/03-summary.md`
- `docs/superpowers/plans/2026-05-29-code-generator-basic.md`

## 执行过程

1. 先新增三个 CodeGenerator API 级测试，并确认接口未实现时返回 404，测试红灯成立。
2. 增加契约、服务、模板渲染器、历史实体和 EF 仓储。
3. 接入预览、默认冲突拦截、安全路径白名单、生成历史。
4. 增加 `系统管理 / 开发工具 / 代码生成` 菜单和 `system:code-generator:*` 权限。
5. 增加 Vben 页面，支持读取表、手工配置字段、预览文件、生成代码、查看历史。
6. 修正测试生成物清理，避免测试污染工作树。
7. 更新菜单断言，纳入新增的 `DevelopmentTools` 目录。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
```

结果：通过，3 个测试全部绿色。

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

结果：通过，125 个测试全部绿色。

```powershell
pnpm run build:antd
```

结果：通过，`@vben/web-antd` 构建成功。

## 当前状态

代码生成器一期已完成可用闭环。真实 MySQL 表读取能力已实现，但还需要启动后端连接你的线上 MySQL 后，在页面上做一次实际表选择和预览联调。
