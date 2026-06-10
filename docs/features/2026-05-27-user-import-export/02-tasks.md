# 用户导入导出任务执行文档

## 任务清单

- [x] 编写导出用户 Excel 的失败测试
- [x] 编写导入用户 Excel 的失败测试
- [x] 新增用户导入导出契约
- [x] 实现内置 xlsx 生成和解析服务
- [x] 后端用户应用服务接入导出、模板、导入
- [x] API 增加导出、模板、导入接口和权限码
- [x] 种子菜单增加导入、导出按钮权限
- [x] 前端用户管理页增加导入、导出按钮
- [x] 写完工总结并验证

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/Users/UserImportExportDtos.cs`
- `src/MiniAdmin.Application.Contracts/Users/IUserImportExportService.cs`
- `src/MiniAdmin.Application.Contracts/Users/IUserAppService.cs`
- `src/MiniAdmin.Application.Contracts/Users/IUserRepository.cs`
- `src/MiniAdmin.Application/Users/UserAppService.cs`
- `src/MiniAdmin.Infrastructure/Users/XlsxUserImportExportService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminSeedIds.cs`
- `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 执行步骤

1. 写导出测试，期望 `/system/user/export` 返回 Excel 文件。
2. 运行测试，确认因接口不存在失败。
3. 写导入测试，期望上传模板数据后创建用户。
4. 运行测试，确认因接口不存在失败。
5. 实现契约、xlsx 服务、应用服务和仓储。
6. 增加 API 路由和权限码。
7. 增加前端按钮、上传、下载逻辑。
8. 跑后端测试和前端构建。

## 当前状态

已完成。

## 验证结果

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminUserImportExportFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：`66 passed`。

```powershell
pnpm run build:antd
```

结果：前端构建通过。

