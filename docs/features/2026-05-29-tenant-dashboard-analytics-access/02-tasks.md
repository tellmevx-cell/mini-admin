# 租户默认分析页访问修复任务

## 任务清单

- [x] 复现 `liqing@jxnc` 登录后进入 `/analytics` 显示 404。
- [x] 确认后端菜单只返回 `Workspace`，未返回 `Analytics`。
- [x] 增加回归测试：新建租户管理员登录后必须有 `Analytics` 菜单。
- [x] 更新租户管理员默认菜单，补入 `Analytics`。
- [x] 增加一次性种子版本，修复历史套餐和历史角色。
- [x] 清理受影响租户用户授权缓存。
- [x] 运行租户和套餐相关测试。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter TenantAdmin_CreateTenant_Creates_Admin_And_Allows_Login
dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "TenantAdmin|TenantPackage|TenantDataIsolation|TenantManagement|Common_System"
```
