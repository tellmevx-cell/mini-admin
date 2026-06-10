# 租户到期时间与登录选项防呆总结

## 根因

新建租户初始化成功后没有出现在登录页，不是模板初始化失败，而是该租户的 `ExpireAt` 已经过期。登录页接口 `/auth/tenant-options` 按设计只返回 `Active` 且未过期的租户。

## 实现内容

- `EfTenantRepository` 新增到期时间校验，新增和编辑租户时拒绝过去时间。
- 租户列表 DTO 返回有效状态：`Active + ExpireAt <= Now` 会展示为 `Expired`。
- 租户列表状态筛选改为按有效状态筛选，避免筛选 `启用` 时混入已过期租户。
- 编辑租户接口补充业务异常处理，返回 `400` 和明确错误消息。
- 前端租户管理到期时间选择器禁用过去日期，并在提交前拦截过去时间。
- 前端到期时间增加 `YYYY-MM-DD HH:mm:ss` 展示格式，以及 `1年后 / 3年后 / 5年后 / 不限制` 快捷设置，方便快速选择 2029 等远期时间。
- 修复 DatePicker 年份面板只切换面板不提交值的问题：监听 `panelChange` 同步面板年份，并增加按年份选择控件，选择 2030 等年份时会落到对应年份的 `12-31 23:59:59`。

## 验证

- `dotnet test C:\monica\code\mini-admin\tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj --filter "PlatformTenant_List_Returns_Expired_When_Active_Tenant_ExpireAt_Passed|PlatformTenant_Create_Rejects_Expired_ExpireAt|PlatformTenant_Update_Rejects_Expired_ExpireAt"`
- 结果：3 个回归测试通过。
- `dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx`
- 结果：122 个后端测试通过。
- `pnpm run build:antd`
- 结果：Vben 前端构建通过。
