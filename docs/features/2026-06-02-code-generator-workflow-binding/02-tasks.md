# 代码生成器接入工作流绑定任务执行文档

## 任务清单

- [x] 扩展代码生成器请求 DTO：增加启用审批和业务类型编码。
- [x] 编写失败测试：开启审批后预览必须包含审批骨架。
- [x] 编写失败测试：开启审批但业务类型为空时必须拒绝。
- [x] 编写失败测试：Persistence 必须支持扫描注册工作流业务状态处理器。
- [x] 扩展后端参数校验和权限码生成。
- [x] 扩展后端模板：实体、DTO、AppService、Repository、API、菜单、建表 SQL。
- [x] 扩展后端模板：生成业务状态回写 `WorkflowStateHandler`。
- [x] 扩展基础设施注册：自动扫描 `IWorkflowBusinessStateHandler` 实现。
- [x] 扩展前端 API 类型。
- [x] 扩展前端代码生成器配置页面。
- [x] 运行后端测试。
- [x] 运行后端构建。
- [x] 运行前端构建。
- [x] 补充总结文档。
- [x] 启动后端和前端。

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminPersistenceServiceCollectionExtensions.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

### 文档

- `docs/features/2026-06-02-code-generator-workflow-binding/01-requirements.md`
- `docs/features/2026-06-02-code-generator-workflow-binding/02-tasks.md`
- `docs/features/2026-06-02-code-generator-workflow-binding/03-summary.md`

## 执行步骤

1. 先写预览测试，断言启用审批后生成结果中包含工作流字段、权限码、后端接口和前端按钮。
2. 运行测试确认失败，失败原因应为模板尚未支持审批。
3. 再写业务类型为空的参数校验测试。
4. 运行测试确认失败，失败原因应为当前校验未阻止空业务类型。
5. 实现 DTO、校验、权限码和模板扩展。
6. 扩展前端配置入口，保证用户能在代码生成器里选择是否生成审批骨架。
7. 运行测试和构建。
8. 补充总结文档。
9. 启动前后端。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "FullyQualifiedName~CodeGenerator" --logger "console;verbosity=minimal"
```

```powershell
dotnet build C:\monica\code\mini-admin\src\MiniAdmin.Api\MiniAdmin.Api.csproj
```

```powershell
pnpm run build:antd
```

## 当前状态

实现、验证和前后端启动已完成。
