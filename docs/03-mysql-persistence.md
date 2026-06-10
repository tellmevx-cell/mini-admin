# 03 - MySQL 持久化与初始化数据

这一阶段把上一节的“假数据登录闭环”换成了真正的持久化结构：

- `Domain` 定义实体：用户、角色、菜单、部门、岗位、字典、参数、通知公告、审计日志、用户角色、角色菜单。
- `Application.Contracts` 定义仓储接口和密码服务接口。
- `Application` 只编排业务流程，不关心数据库类型。
- `Infrastructure` 用 EF Core 实现仓储，并决定使用 InMemory 还是 MySQL。
- `Api` 在启动时注册持久化层，并执行初始化数据。

## 当前初始化数据

启动时会执行 `MiniAdminDatabaseInitializer`，如果不存在 `admin` 用户，就写入第一批数据：

- 用户：`admin / 123456`
- 角色：`admin`
- 菜单：
  - `Dashboard`
  - `Analytics`
  - `Workspace`
  - `System`
  - `TenantPackage`
  - `TenantManagement`
  - `UserManagement`
  - `FileManagement`
  - `RoleManagement`
  - `MenuManagement`
  - `DepartmentManagement`
  - `PositionManagement`
  - `DictionaryManagement`
  - `ParameterSetting`
  - `NoticeAnnouncement`
  - `LogManagement`
- 权限码：
  - `system:dashboard:analytics`
  - `system:dashboard:workspace`
  - `system:user:query`
  - `system:tenant-package:query`
  - `system:tenant:query`
  - `system:file:query`
  - `system:file:upload`
  - `system:file:download`
  - `system:file:delete`
  - `system:role:query`
  - `system:menu:query`
  - `system:department:query`
  - `system:position:query`
  - `system:dictionary:query`
  - `system:parameter:query`
  - `system:notice:query`
  - `system:log:query`

部门数据包含：

- `hq`：总部
- `rd`：研发部，隶属于总部
- `ops`：运营部，隶属于总部

岗位数据包含：

- `manager`：管理员
- `developer`：开发工程师

字典数据包含：

- `user_status`：用户状态
  - `1`：启用
  - `0`：停用

参数数据包含：

- `site_name`：站点名称
- `default_password`：默认密码示例

用户数据包含：

- `admin / 123456`，启用，归属总部
- `demo / 123456`，启用，归属总部
- `auditor / 123456`，停用，归属总部

其中 `UserQueryPermission` 是一个“权限点”，`IsVisible = false`，会出现在 `/auth/codes`，但不会单独出现在 `/menu/all` 的 Vben 菜单树里。`UserManagement` 是可见菜单，也使用 `system:user:query` 权限码。

## 默认配置

当前 `appsettings.json` 默认使用内存库，方便测试和学习：

```json
{
  "Database": {
    "Provider": "InMemory",
    "InitializeOnStartup": true,
    "InMemoryDatabaseName": "MiniAdmin",
    "MySqlServerVersion": "8.0.36-mysql"
  },
  "ConnectionStrings": {
    "MiniAdmin": ""
  }
}
```

## 切换到 MySQL

不要把线上库密码写进 Git。建议用环境变量启动：

```powershell
$env:Database__Provider = "MySql"
$env:ConnectionStrings__MiniAdmin = "Server=你的主机;Port=3306;Database=mini_admin;User=你的用户;Password=你的密码;CharSet=utf8mb4;"
dotnet run --project src\MiniAdmin.Api\MiniAdmin.Api.csproj --urls http://localhost:5320
```

如果你的 MySQL 版本不是 8.0，可以同时设置：

```powershell
$env:Database__MySqlServerVersion = "8.0.36-mysql"
```

首次连接时，当前阶段使用 `EnsureCreatedAsync()` 创建表并写入初始化数据。因为你已经有一个旧的 MySQL 库，后续新增实体时 `EnsureCreatedAsync()` 不会自动补表，所以部门、岗位、字典、参数等新表暂时增加了轻量的 `CREATE TABLE IF NOT EXISTS` 兜底。后续进入正式开发时，我们会把它升级成 EF Core Migrations，这样数据库结构变更可以留下迁移历史。

## 为什么这样设计方便扩展其他数据库

业务层只依赖这些接口：

- `IAuthRepository`
- `IUserRepository`
- `IMenuRepository`
- `IRoleRepository`
- `IDepartmentRepository`
- `IPositionRepository`
- `IDictionaryRepository`
- `ISystemParameterRepository`
- `IAuditLogRepository`
- `IPasswordService`

MySQL 只出现在 `Infrastructure` 的 `AddMiniAdminPersistence()` 里。以后要支持 SQL Server 或 PostgreSQL，主要步骤是：

1. 安装对应 EF Core Provider。
2. 在 `AddMiniAdminPersistence()` 里增加一个 `Provider` 分支。
3. 保持 Application 和 Api 的业务代码不变。

## 本阶段验证命令

```powershell
dotnet test tests\MiniAdmin.Tests\MiniAdmin.Tests.csproj
dotnet build MiniAdmin.slnx
```

测试重点：

- 登录返回真实 JWT。
- 受保护接口没有 token 会返回 401。
- `admin / 123456` 可以登录。
- 错误密码会被拒绝。
- `/auth/codes` 返回初始化权限码。
- `/menu/all` 返回 Vben 可用的 Dashboard 菜单树。
- `/system/user/list` 返回初始化用户分页数据。
- 写操作会记录审计日志，请求体中的密码和 token 类字段会被脱敏。

## 第一个业务列表：用户管理

后端接口：

```http
GET /system/user/list?page=1&pageSize=10&userName=demo
POST /system/user
PUT /system/user/{id}
DELETE /system/user/{id}
Authorization: Bearer <access-token>
```

返回结构：

```json
{
  "code": 0,
  "data": {
    "items": [
      {
        "id": "10000000-0000-0000-0000-000000000002",
        "userName": "demo",
        "realName": "Demo User",
        "departmentId": "40000000-0000-0000-0000-000000000001",
        "departmentName": "总部",
        "roles": ["admin"],
        "status": 1
      }
    ],
    "total": 1
  },
  "message": "ok"
}
```

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/user/index.vue
```

Vben 菜单由后端 `/menu/all` 返回，`UserManagement` 的组件路径是 `/system/user/index`，会映射到上面的 Vue 文件。

用户列表现在已经接入部门和岗位归属：

- `mini_users.DepartmentId` 关联 `mini_departments.Id`。
- `mini_users.PositionId` 关联 `mini_positions.Id`。
- `/system/user/list` 返回 `departmentId`、`departmentName`、`positionId` 和 `positionName`。
- Vben 用户管理页展示“所属部门”列。
- Vben 用户管理页展示“所属岗位”列。
- 新增和编辑用户时，可以选择所属部门、所属岗位、分配角色、设置启停状态。
- 用户列表支持按用户名、部门、岗位组合筛选。

因为这是在已有 MySQL 库上新增字段，当前阶段启动时会轻量检查并补齐 `mini_users.DepartmentId`、`mini_users.PositionId`、索引和外键。正式开发时这部分也会迁移到 EF Core Migrations。

用户管理 CRUD 的请求模型：

```json
{
  "userName": "zhangsan",
  "realName": "张三",
  "password": "123456",
  "departmentId": "40000000-0000-0000-0000-000000000001",
  "positionId": "70000000-0000-0000-0000-000000000001",
  "roleIds": ["20000000-0000-0000-0000-000000000001"],
  "isEnabled": true
}
```

编辑用户时不允许改用户名；`password` 可以为空，表示不重置密码。删除规则上，初始化的 `admin` 用户不能删除。

用户列表筛选参数：

```http
GET /system/user/list?page=1&pageSize=10&userName=demo&departmentId=40000000-0000-0000-0000-000000000001&positionId=70000000-0000-0000-0000-000000000001
```

## 系统管理菜单骨架

为了先把后台管理的导航结构打通，当前阶段已经补齐一组常见系统管理入口：

```text
租户套餐
租户管理
用户管理
文件管理
角色管理
菜单管理
部门管理
岗位管理
字典管理
参数设置
通知公告
日志管理
```

除用户管理外，其余页面先是 Vben 页面骨架，后续会按“角色管理 -> 菜单管理 -> 部门管理 -> 字典管理”的顺序逐步接入真实 CRUD。

## 角色管理 CRUD

角色管理已经从页面骨架升级为基础 CRUD：

```http
GET /system/role/list?page=1&pageSize=10&code=admin
POST /system/role
PUT /system/role/{id}
DELETE /system/role/{id}
```

当前字段：

- `code`：角色编码，创建后不在编辑页修改。
- `name`：角色名称。
- `isEnabled` / `status`：启停状态。

删除规则：

- `admin` 角色不能删除。
- 已绑定用户的角色不能删除。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/role/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/role.ts
```

下一步会在这个基础上增加“角色分配菜单权限”，也就是把 `mini_roles` 和 `mini_menus` 通过 `mini_role_menus` 在页面上维护起来。

## 文件管理与存储切换

文件管理已经从页面骨架升级为基础上传、列表、下载和删除：

```http
GET /system/file/list?page=1&pageSize=10&originalName=demo&storageProvider=local
POST /system/file/upload
GET /system/file/{id}/download
DELETE /system/file/{id}
```

表结构：

- `mini_files`

当前记录字段：

- 原始文件名 `originalName`
- 存储文件名 `storedName`
- 内容类型 `contentType`
- 文件大小 `size`
- 存储方式 `storageProvider`：`local` 或 `minio`
- 存储路径 `storagePath`
- 上传时间 `createdAt`

存储配置在 `FileStorage` 节里：

```json
{
  "FileStorage": {
    "Provider": "Local",
    "Local": {
      "RootPath": "storage/uploads"
    },
    "Minio": {
      "Endpoint": "http://127.0.0.1:9000",
      "AccessKey": "你的 AccessKey",
      "SecretKey": "你的 SecretKey",
      "Bucket": "mini-admin",
      "Region": "us-east-1",
      "UseSsl": false
    }
  }
}
```

切换到 MinIO 时，把 `Provider` 改成 `Minio`，并填写 MinIO 配置。业务层只依赖 `IFileStorageService`，不关心文件最终写到本地磁盘还是对象存储；后续要接阿里云 OSS、腾讯云 COS，也是在 Infrastructure 增加新的存储实现。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/file/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/file.ts
```

文件上传是 `multipart/form-data`，审计日志不会记录完整文件体，只记录 `[multipart/form-data omitted]`，避免把文件内容写进审计表。

文件管理已经开始接入细粒度权限：

- `system:file:query`：查看文件列表。
- `system:file:upload`：上传文件。
- `system:file:download`：下载文件。
- `system:file:delete`：删除文件。

后端接口会按权限码返回 403；Vben 文件管理页也会按 `/auth/codes` 返回的权限码隐藏上传、下载和删除按钮。

## 角色分配菜单权限

角色管理已经接入菜单权限分配：

```http
GET /system/menu/tree
GET /system/role/{id}/menus
PUT /system/role/{id}/menus
```

数据流：

1. Vben 角色管理页点击“分配权限”。
2. 前端请求 `/system/menu/tree` 获取完整菜单树。
3. 前端请求 `/system/role/{id}/menus` 获取该角色已勾选的菜单 ID。
4. 保存时调用 `PUT /system/role/{id}/menus`。
5. 后端重写 `mini_role_menus` 中该角色的菜单关系。

这一步打通了后台权限体系的核心关系：`用户 -> 角色 -> 菜单/权限码`。

## 菜单管理 CRUD

菜单管理已经从页面骨架升级为基础 CRUD：

```http
GET /system/menu/list
POST /system/menu
PUT /system/menu/{id}
DELETE /system/menu/{id}
```

当前可维护字段：

- 上级菜单
- 路由名称 `name`
- 路径 `path`
- 组件路径 `component`
- 重定向 `redirect`
- 菜单标题 `title`
- 图标 `icon`
- 排序 `order`
- 权限码 `permissionCode`
- 启用状态 `isEnabled`
- 显示状态 `isVisible`
- 固定标签 `affixTab`

删除规则：

- 有子菜单的菜单不能删除。
- 已经分配给角色的菜单不能删除。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/menu/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/menu.ts
```

菜单管理让后续开发不再依赖硬编码种子数据。新增菜单后，再通过角色管理的“分配权限”把菜单授权给角色，用户重新登录后即可按角色获取对应菜单。

## 部门管理 CRUD

部门管理已经从页面骨架升级为基础 CRUD：

```http
GET /system/department/list
POST /system/department
PUT /system/department/{id}
DELETE /system/department/{id}
```

当前可维护字段：

- 上级部门
- 部门编码 `code`
- 部门名称 `name`
- 负责人 `leader`
- 联系电话 `phone`
- 排序 `order`
- 启用状态 `isEnabled`

删除规则：

- 有子部门的部门不能删除。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/department/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/department.ts
```

这一节先把组织架构树维护起来。后续做用户管理增强时，可以把 `mini_users` 扩展出 `DepartmentId`，让用户归属到部门，再继续做“按部门筛选用户”和“部门下用户列表”。

## 字典管理 CRUD

字典管理已经从页面骨架升级为基础 CRUD：

```http
GET /system/dictionary/list
POST /system/dictionary/type
PUT /system/dictionary/type/{id}
DELETE /system/dictionary/type/{id}
POST /system/dictionary/item
PUT /system/dictionary/item/{id}
DELETE /system/dictionary/item/{id}
```

表结构分两层：

- `mini_dictionary_types`：字典类型，例如 `user_status`。
- `mini_dictionary_items`：字典项，例如 `1 = 启用`、`0 = 停用`。

字典类型字段：

- 编码 `code`
- 名称 `name`
- 排序 `order`
- 启用状态 `isEnabled`

字典项字段：

- 所属类型 `typeId`
- 选项名称 `label`
- 选项值 `value`
- 颜色 `color`
- 排序 `order`
- 启用状态 `isEnabled`

删除规则：

- 字典类型下面还有字典项时，不能删除类型。
- 字典项可以直接删除。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/dictionary/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/dictionary.ts
```

这一步的重点是“把业务枚举从代码里挪到数据库里”。以后用户状态、通知类型、日志类型、文件分类等，都可以统一从字典模块取值，避免前端和后端各写一份枚举导致不一致。

## 参数设置 CRUD

参数设置已经从页面骨架升级为基础 CRUD：

```http
GET /system/parameter/list?page=1&pageSize=10&key=site
POST /system/parameter
PUT /system/parameter/{id}
DELETE /system/parameter/{id}
```

表结构：

- `mini_system_parameters`

当前可维护字段：

- 参数键名 `key`
- 参数名称 `name`
- 参数键值 `value`
- 参数分组 `group`
- 备注 `remark`
- 排序 `order`
- 启用状态 `isEnabled`

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/parameter/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/parameter.ts
```

参数设置和字典管理的区别：

- 字典管理解决“可选项”，例如用户状态、通知类型。
- 参数设置解决“系统配置”，例如站点名称、默认密码、上传限制、开关配置。

当前阶段参数会直接读写数据库。后续可以再加一层缓存和“刷新缓存”接口，避免频繁查询数据库。

## 通知公告 CRUD

通知公告已经从页面骨架升级为基础 CRUD：

```http
GET /system/notice/list?page=1&pageSize=10&title=MiniAdmin&type=notice&isPublished=true
POST /system/notice
PUT /system/notice/{id}
DELETE /system/notice/{id}
```

表结构：

- `mini_notices`

当前可维护字段：

- 标题 `title`
- 类型 `type`：`notice` 通知、`announcement` 公告
- 内容 `content`
- 发布状态 `isPublished`
- 发布时间 `publishedAt`
- 创建时间 `createdAt`

初始化数据包含：

- `欢迎使用 MiniAdmin`

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/notice/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/notice.ts
```

这一步先实现“公告本身”的维护。后续如果要做按租户、角色或用户定向发布，可以在 `mini_notices` 之外增加发布范围表，而不是把范围字段硬塞进公告主表。

## 审计日志

审计日志已经接入请求中间件，会自动追踪后台写操作：

```http
POST /auth/login
POST /system/user
PUT /system/user/{id}
DELETE /system/user/{id}
...
```

查询接口：

```http
GET /system/audit-log/list?page=1&pageSize=10&userName=admin&method=POST&path=/system/user
GET /system/audit-log/list?startCreatedAt=2026-05-26T00:00:00Z&endCreatedAt=2026-05-26T23:59:59Z
GET /system/audit-log/export?method=POST&path=/system/user
```

表结构：

- `mini_audit_logs`

当前记录字段：

- 操作人：`userId`、`userName`
- 请求信息：`method`、`path`、`queryString`、`requestBody`
- 业务归类：`module`、`action`、`resourceId`
- 响应信息：`statusCode`、`isSuccess`、`elapsedMilliseconds`、`errorMessage`
- 来源信息：`ipAddress`、`userAgent`
- 时间：`createdAt`

列表和导出都支持按操作人、HTTP 方法、路径、模块、动作、成功状态和创建时间范围筛选。导出格式为 CSV，最多导出 5000 条，文件带 UTF-8 BOM，方便用 Excel 打开中文内容。

审计日志保留最近 90 天。系统启动执行 `MiniAdminDatabaseInitializer` 时，会自动删除 `CreatedAt` 早于 90 天前的审计日志，不提供手动删除入口，避免人为抹除近期操作轨迹。

请求体会保留，但会做两层保护：

- 敏感字段脱敏：`password`、`oldPassword`、`newPassword`、`confirmPassword`、`token`、`accessToken`、`refreshToken`、`authorization`、`secret`、`signingKey`。
- 最大长度截断为 4000 字符，避免异常大请求把日志表撑爆。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/log/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/audit-log.ts
```

这一步让系统能回答“谁在什么时候对哪个资源做了什么、传了什么数据、是否成功”。后续可以继续补登录失败原因、导出、清理策略、按租户隔离，以及把查询权限细化成 `system:audit-log:query`。

## 岗位管理 CRUD

岗位管理已经从页面骨架升级为基础 CRUD：

```http
GET /system/position/list?page=1&pageSize=10&code=manager
POST /system/position
PUT /system/position/{id}
DELETE /system/position/{id}
```

表结构：

- `mini_positions`

当前可维护字段：

- 岗位编码 `code`
- 岗位名称 `name`
- 排序 `order`
- 备注 `remark`
- 启用状态 `isEnabled`

删除规则：

- 已经被用户绑定的岗位不能删除。

前端页面：

```text
frontend/vue-vben-admin/apps/web-antd/src/views/system/position/index.vue
```

前端接口：

```text
frontend/vue-vben-admin/apps/web-antd/src/api/system/position.ts
```

这一节先把岗位字典维护起来，并已经在用户管理里接入 `PositionId`。用户现在可以同时归属部门和岗位，用户列表也支持按部门/岗位组合筛选。删除岗位前会检查是否已有用户绑定，避免留下失效的岗位引用。
