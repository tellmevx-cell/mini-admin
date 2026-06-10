# 旧租户管理入口清理任务

## 任务清单

- [x] 复现并确认旧入口残留问题。
- [x] 增加回归测试：旧 `系统管理 / 租户管理` 被授权时，初始化后应自动清理。
- [x] 增加一次性数据种子版本 `202605290004-legacy-system-tenant-management-cleanup`。
- [x] 禁用并隐藏旧菜单 `TenantManagement`，路径 `/system/tenant`。
- [x] 移除旧菜单及其子权限的角色菜单授权。
- [x] 清理 admin 授权缓存，避免刷新后继续看到旧菜单。
- [x] 运行回归测试。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter DatabaseInitializer_Removes_Legacy_SystemTenantManagement_Menu
```

## 预期结果

- `系统管理` 下不再显示 `租户管理`。
- `平台管理` 下继续显示 `租户管理`。
- `/platform/tenant/list` 继续可用。
