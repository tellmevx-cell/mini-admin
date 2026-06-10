# 通用导入导出与代码生成器联动任务

## 任务拆分

- [ ] 编写岗位导入导出集成测试，先确认测试失败。
- [ ] 抽出通用 Excel workbook 服务，替代用户专用接口的重复命名。
- [ ] 增加岗位导入导出 DTO、应用服务和仓储能力。
- [ ] 增加岗位导出、模板、预检、确认导入、错误报告 API。
- [ ] 增加岗位导入导出权限种子并授予 Admin。
- [ ] 前端岗位页面接入导出、模板下载、导入预检弹窗和失败明细下载。
- [ ] 代码生成器增加导入导出开关。
- [ ] 生成器模板输出导入导出接口、权限、前端按钮和文案。
- [ ] 补总结文档。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj -c Release --filter "PositionImportExport|CodeGenerator"
```

```powershell
npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\position\index.vue
```

```powershell
npx impeccable --json frontend\vue-vben-admin\apps\web-antd\src\views\system\code-generator\index.vue
```

```powershell
pnpm run build:antd
```

