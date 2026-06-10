# 代码生成器一期总结

## 完成内容

本次完成代码生成器一期闭环，定位是企业级后台的受控工程生产线，而不是任意拖拽式低代码。

已支持：

- 读取 MySQL 当前库的 `information_schema.tables` 和 `information_schema.columns`。
- 手工配置模块名、业务名称、路由、权限前缀、租户模式。
- 配置字段的列表、查询、新增、编辑、必填、控件类型。
- 预览生成文件、权限码和冲突状态。
- 默认禁止覆盖已有文件。
- 生成成功或失败都会写入历史记录。
- 后端接口使用 RBAC 权限保护。
- 前端新增 `系统管理 / 开发工具 / 代码生成` 页面。

## API

- `GET /system/code-generator/tables`
- `GET /system/code-generator/tables/{tableName}`
- `POST /system/code-generator/preview`
- `POST /system/code-generator/generate`
- `GET /system/code-generator/history`

## 权限码

- `system:code-generator:query`
- `system:code-generator:preview`
- `system:code-generator:generate`

## 生成文件范围

一期预览和生成以下文件：

- Domain Entity
- Contracts DTO、Query、SaveRequest、AppService 接口、Repository 接口
- Application AppService
- Infrastructure EF Repository
- Vben API TS
- Vben 列表页

## 安全约束

- 只允许写入项目内固定白名单目录。
- 禁止绝对路径。
- 禁止 `..` 路径穿越。
- 禁止反斜杠混入生成路径。
- 默认不覆盖已有文件。
- 生成历史记录请求配置、文件列表、状态和错误信息。

## 验证结果

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "CodeGenerator"
```

结果：通过，3 个测试。

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

结果：通过，125 个测试。

```powershell
pnpm run build:antd
```

结果：通过。

## 下一步建议

下一阶段建议做“生成代码二期”：把生成结果从骨架提升到真正可编译、可运行的完整 CRUD 模块，包括 DbContext 注册、Minimal API endpoint 生成、菜单权限生成、租户过滤、字典控件绑定和前端新增/编辑弹窗。
