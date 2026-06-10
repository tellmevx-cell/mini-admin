# 代码生成器第二阶段总结

## 完成内容

- 生成接口新增 `autoInstall` 开关，默认开启。
- 生成成功后会自动执行数据库侧安装：
  - MySQL 且目标表不存在时执行建表脚本。
  - 创建或更新业务菜单。
  - 创建或更新查询、新增、编辑、删除按钮权限。
  - 授予 Admin 角色。
  - 清理 Admin 授权缓存。
- 安装指引新增自动安装和菜单权限状态。
- Vben 代码生成器页面新增“生成后自动安装数据库表和菜单权限”开关。
- 保留后端重启提示，因为新增 C# Endpoint、仓储、服务和 EF 配置仍需要重新编译加载。

## 关键文件

- `src/MiniAdmin.Application.Contracts/CodeGenerators/CodeGeneratorDtos.cs`
- `src/MiniAdmin.Application.Contracts/CodeGenerators/ICodeGeneratorRepository.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorAppService.cs`
- `src/MiniAdmin.Application/CodeGenerators/CodeGeneratorTemplateRenderer.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfCodeGeneratorRepository.cs`
- `frontend/vue-vben-admin/apps/web-antd/src/api/system/code-generator.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/code-generator/index.vue`
- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 验证结果

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj -c Release --filter "CodeGenerator"
```

结果：12 个生成器相关测试全部通过。

```powershell
npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue
```

结果：`[]`

```powershell
pnpm run build:antd
```

结果：11 个前端构建任务成功。环境仍提示 `Requested version v22.22.0 is not currently installed`，但命令退出码为 0。

## 使用说明

在代码生成器页面预览确认后，默认保持“生成后自动安装数据库表和菜单权限”开启，再点击生成。

生成完成后：

- 数据库表和菜单权限会立即安装。
- Admin 角色会立即拥有新模块权限。
- 仍需要重启后端，新增接口才会真正挂载到运行时。
