# 用户导入预检与失败明细任务执行文档

## 任务清单

- [x] 编写预检不落库失败测试
- [x] 编写失败明细下载失败测试
- [x] 后端契约增加预检和错误明细能力
- [x] Repository 拆出校验逻辑，支持 dry-run
- [x] API 增加预检和失败明细接口
- [x] 前端导入流程改为预检弹窗和确认导入
- [x] 写完工总结并验证

## 涉及文件

### 后端

- `src/MiniAdmin.Application.Contracts/Users/UserImportExportDtos.cs`
- `src/MiniAdmin.Application.Contracts/Users/IUserAppService.cs`
- `src/MiniAdmin.Application.Contracts/Users/IUserRepository.cs`
- `src/MiniAdmin.Application/Users/UserAppService.cs`
- `src/MiniAdmin.Infrastructure/Persistence/EfUserRepository.cs`
- `src/MiniAdmin.Api/Program.cs`

### 前端

- `frontend/vue-vben-admin/apps/web-antd/src/api/system/user.ts`
- `frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue`

### 测试

- `tests/MiniAdmin.Tests/VbenLoginLoopTests.cs`

## 当前状态

已完成。

## 验证记录

### 后端测试

```powershell
$env:Database__Provider='InMemory'
$env:Database__InMemoryDatabaseName='MiniAdminUserImportPreviewFull'
$env:Cache__Provider='Memory'
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
```

结果：68 个测试通过。

### 前端构建

```powershell
pnpm run build:antd
```

执行目录：`frontend/vue-vben-admin`

结果：Vben Ant Design 应用构建通过。
