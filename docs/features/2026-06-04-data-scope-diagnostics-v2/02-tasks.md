# 数据权限 2.0 诊断增强任务执行文档

## 任务清单

- [x] 为权限诊断补一条失败测试，先锁定“返回部门名称”行为。
- [x] 扩展后端 DTO，增加数据范围部门名称和角色自定义部门名称。
- [x] 在权限诊断仓储中解析部门名称。
- [x] 更新前端 API 类型。
- [x] 优化权限诊断页展示。
- [x] 执行后端测试验证。
- [x] 执行前端生产构建验证。

## 执行步骤

1. 在 `VbenLoginLoopTests` 中新增组合范围权限诊断测试。
2. 让测试先失败，确认当前接口没有返回部门名称。
3. 扩展 `PermissionDiagnosticsDto`：
   - `PermissionDiagnosticsDataScopeDto.DepartmentNames`
   - `PermissionDiagnosticsRoleDto.CustomDepartmentNames`
4. 在 `EfPermissionDiagnosticsRepository` 中聚合部门 ID，并回查部门名称。
5. 更新前端 `permission-diagnostics.ts` 类型定义。
6. 调整权限诊断页：
   - 数据权限区域展示名称标签
   - 角色区域展示自定义部门标签
7. 重新运行测试和前端构建。

## 执行命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "FullyQualifiedName~PermissionDiagnostics_Returns_DataScope_Department_Names_For_Mixed_Scope"
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --no-build --filter "FullyQualifiedName~PermissionDiagnostics_|FullyQualifiedName~RoleCreate_WithCustomScope_Requires_Department_Selection|FullyQualifiedName~DataScopeProvider_Resolves_Custom_Department_And_Mixed_Scope|FullyQualifiedName~LoginLogList_Applies_Department_DataScope_From_Current_User_Roles|FullyQualifiedName~OnlineUserList_Applies_Department_DataScope_From_Current_User_Roles|FullyQualifiedName~SecurityEventList_Applies_Department_DataScope_From_Current_User_Roles"
pnpm run build:antd
```

## 结果

- 新增权限诊断测试先红后绿。
- 9 条相关后端集成测试通过。
- `pnpm run build:antd` 通过。
