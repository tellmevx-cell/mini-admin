# 代码生成器第三阶段任务执行文档

## 任务拆分

- [x] 增加回滚 DTO、应用服务接口和仓储接口。
- [x] 增加 `system:code-generator:rollback` 权限种子。
- [x] 增加回滚接口和权限控制。
- [x] 实现删除生成文件、清理空目录、删除菜单权限、更新历史状态。
- [x] 支持回滚时可选删除业务表和表内数据，默认保留。
- [x] 支持已回滚记录继续清理业务表。
- [x] 前端生成记录和详情增加回滚按钮，并提供危险删表勾选项。
- [x] 补回滚测试。
- [x] 补总结文档。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj -c Release --filter "CodeGenerator"
```

```powershell
npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue
```

```powershell
pnpm run build:antd
```
