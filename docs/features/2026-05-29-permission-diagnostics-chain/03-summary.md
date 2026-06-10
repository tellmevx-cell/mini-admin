# 权限诊断链路增强总结

## 实现内容

- 后端权限诊断 DTO 增加 `tenant`、`effective`、`warnings` 三组数据。
- 后端诊断仓储按真实授权链路计算：
  - 用户启用角色
  - 角色分配菜单
  - 租户套餐菜单上限
  - 角色菜单与套餐菜单交集
  - 最终菜单、按钮权限、权限码数量
- 每个角色返回菜单数量、可见菜单数量、按钮权限数量，方便判断角色本身是否有授权。
- 增加诊断提示：
  - 用户禁用
  - 没有启用角色
  - 启用角色没有菜单
  - 租户套餐没有菜单
  - 角色菜单被套餐全部过滤
- 前端 `系统监控 / 权限诊断` 页面改为链路看板：
  - 顶部展示“角色菜单 -> 套餐范围 -> 最终菜单”的结果链路
  - 展示诊断告警或正常提示
  - 展示用户、租户套餐、有效权限、数据权限和缓存 key
  - 角色区域改为表格，显示每个角色的菜单贡献

## 验证结果

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter PermissionDiagnostics`
  - 结果：3 passed
- `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`
  - 结果：118 passed
- `pnpm run build:antd`
  - 结果：构建成功
- 本地服务：
  - 后端：`http://localhost:5320/health` 返回 Healthy
  - 前端：`http://localhost:5666/` 返回 200
- 真实接口抽查：
  - `/system/permission-diagnostics/user/liqing`
  - 返回租户 `jxnc`、套餐 `默认套餐`、套餐菜单数 30、最终菜单数 30、告警为空。

## 后续建议

- 后续如果继续做“套餐/角色授权排障”，可以在诊断页增加只读菜单明细对比：角色有但套餐没有、套餐有但角色没有。
- 暂时不做自动修复，避免诊断功能绕过 RBAC 或租户套餐边界。
