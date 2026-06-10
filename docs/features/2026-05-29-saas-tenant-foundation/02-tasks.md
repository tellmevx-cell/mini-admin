# SaaS 租户底座任务执行文档

## 当前状态

- 状态：需求设计阶段
- 本文档只拆解任务，不代表已经开始编码。
- 下一步需要用户确认需求文档后，再进入实现。

## 阶段拆分

### 第一阶段：租户底座

- [x] 新增租户实体 `Tenant`。
- [x] 新增租户套餐实体 `TenantPackage`。
- [x] 新增租户隔离接口 `IHasTenant`。
- [x] 新增当前租户上下文 `CurrentTenant`。
- [x] 登录接口支持租户编码。
- [x] JWT 写入 `tenant_id` 和 `tenant_code`。
- [x] JWT 校验阶段检查租户状态。
- [x] 新增租户禁用后会话失效能力。
- [x] MySQL 初始化脚本补齐租户表和核心系统表 `TenantId` 字段。
- [x] 为租户底座补充集成测试。

### 第二阶段：核心系统表租户隔离

- [x] 用户表支持 `TenantId`。
- [ ] 角色表支持 `TenantId`。
- [ ] 用户角色关系支持租户边界校验。
- [ ] 部门表支持 `TenantId`。
- [ ] 岗位表支持 `TenantId`。
- [ ] 字典类型和字典项支持 `TenantId`。
- [ ] 参数配置支持 `TenantId`。
- [ ] 文件记录支持 `TenantId`。
- [ ] 查询、新增、编辑、删除接口接入租户上下文。
- [ ] 导入导出、文件下载、批量操作校验租户边界。

### 第三阶段：平台租户管理

- [ ] 新增平台管理菜单。
- [ ] 新增租户管理页面。
- [ ] 支持租户新增、编辑、启用、禁用。
- [ ] 支持初始化租户管理员。
- [ ] 支持设置租户过期时间。
- [ ] 支持租户套餐选择。
- [ ] 支持平台管理员代入租户。
- [ ] 支持退出代入租户。
- [ ] 代入状态在前端顶部明显展示。
- [ ] 租户管理和代入操作写审计日志。

### 第四阶段：租户套餐与授权预留

- [ ] 新增租户套餐管理页面。
- [ ] 支持最大用户数配置。
- [ ] 支持最大存储空间配置。
- [ ] 支持套餐可用菜单或功能配置。
- [ ] 登录和用户新增时校验用户数量限制。
- [ ] 文件上传时预留存储空间限制校验。

### 第五阶段：代码生成器租户感知预留

- [ ] 代码生成器需求文档中加入租户配置项。
- [ ] 生成配置支持“平台级模块”和“租户级模块”。
- [ ] 生成的后端查询自动接入租户过滤。
- [ ] 生成的前端页面默认隐藏 `TenantId` 输入。
- [ ] 生成权限码时区分 `platform:*` 和租户业务权限。

## 涉及后端文件规划

| 文件或目录 | 操作 | 说明 |
| --- | --- | --- |
| `src/MiniAdmin.Domain/Entities/Tenant.cs` | 新增 | 租户实体 |
| `src/MiniAdmin.Domain/Entities/TenantPackage.cs` | 新增 | 租户套餐实体 |
| `src/MiniAdmin.Domain/Entities/*` | 修改 | 核心租户级实体增加 `TenantId` |
| `src/MiniAdmin.Application.Contracts/Tenants/*` | 新增 | 租户 DTO、查询、请求模型 |
| `src/MiniAdmin.Application.Contracts/MultiTenancy/*` | 新增 | 当前租户接口和租户隔离契约 |
| `src/MiniAdmin.Application/Tenants/*` | 新增 | 租户应用服务 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDbContext.cs` | 修改 | 租户实体映射和过滤策略 |
| `src/MiniAdmin.Infrastructure/Persistence/MiniAdminDatabaseInitializer.cs` | 修改 | MySQL 初始化和兼容升级 |
| `src/MiniAdmin.Infrastructure/Persistence/Ef*Repository.cs` | 修改 | 核心仓储接入租户过滤 |
| `src/MiniAdmin.Application/Auth/AuthAppService.cs` | 修改 | 登录识别租户 |
| `src/MiniAdmin.Infrastructure/Auth/JwtTokenService.cs` | 修改 | JWT 加入租户 claim |
| `src/MiniAdmin.Api/Program.cs` | 修改 | 接口、鉴权、租户上下文注入 |
| `tests/MiniAdmin.Tests/*` | 修改 | 增加多租户集成测试 |

## 涉及前端文件规划

| 文件或目录 | 操作 | 说明 |
| --- | --- | --- |
| `frontend/vue-vben-admin/apps/web-antd/src/api/core/auth.ts` | 修改 | 登录参数增加租户编码 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/_core/authentication/login.vue` | 修改 | 登录页增加租户编码输入 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/platform/tenant.ts` | 新增 | 租户管理 API |
| `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant/*` | 新增 | 租户管理页面 |
| `frontend/vue-vben-admin/apps/web-antd/src/views/platform/tenant-package/*` | 新增 | 租户套餐页面 |
| `frontend/vue-vben-admin/apps/web-antd/src/store/auth.ts` | 修改 | 保存租户和代入租户状态 |
| `frontend/vue-vben-admin/apps/web-antd/src/router/routes/*` | 修改 | 增加平台管理菜单路由 |
| `frontend/vue-vben-admin/apps/web-antd/src/api/request.ts` | 修改 | 请求头携带代入租户上下文 |

## 测试计划

后端测试重点：

- 平台管理员不填租户编码可以登录。
- 租户管理员填写正确租户编码可以登录。
- 租户编码错误时登录失败。
- 租户禁用或过期时登录失败。
- 租户 A 不能查询租户 B 的用户、角色、部门数据。
- 租户 A 不能删除租户 B 的数据。
- 平台管理员代入租户 A 后只看到租户 A 数据。
- 禁用租户后，该租户在线会话失效。
- 审计日志记录平台代入租户操作。

前端验证重点：

- 登录页租户编码输入符合当前 Vben 风格。
- 平台管理员能看到平台管理菜单。
- 租户管理员看不到平台管理菜单。
- 租户列表支持查询、新增、编辑、启用、禁用。
- 代入租户后顶部有明确提示。
- 退出代入后恢复平台上下文。

## 验证命令

```powershell
dotnet test C:\monica\code\mini-admin\MiniAdmin.slnx
```

```powershell
pnpm run build:antd
```

## 风险点

- 租户过滤如果只靠手写 `Where`，很容易遗漏，必须抽象成统一能力。
- 平台管理员和租户管理员的身份边界不能混淆。
- 旧数据迁移需要给现有单租户数据分配一个默认租户，或者明确作为平台数据处理。
- 前端缓存中已有菜单、权限、用户信息，需要在切换租户或代入租户时清理。
- 禁用租户后，如果只拦截登录，不处理已登录 token，会留下安全口子。

## 推荐实施顺序

1. 先写测试定义租户登录、租户隔离和禁用租户行为。
2. 新增租户实体、上下文和 JWT 租户 claim。
3. 改造登录和鉴权。
4. 改造用户、角色、部门三个最核心表。
5. 做平台租户管理页面。
6. 补齐字典、参数、文件等租户级资源。
7. 做平台代入租户。
8. 补租户套餐和代码生成器预留。
