# MiniAdmin 项目初始化与分层说明

这份文档记录 MiniAdmin 从零创建到当前解决方案结构的过程，帮助你理解每个项目为什么存在、它们之间如何引用，以及下一步应该按什么顺序继续学习和开发。

## 1. 当前项目状态

当前仓库根目录是：

```powershell
C:\monica\code\mini-admin
```

解决方案文件：

```text
MiniAdmin.slnx
```

`src` 目录下已有 7 个项目，并且目标框架都是 `net10.0`：

```text
src/
  MiniAdmin.Api/
  MiniAdmin.Application/
  MiniAdmin.Application.Contracts/
  MiniAdmin.Domain/
  MiniAdmin.Domain.Shared/
  MiniAdmin.Infrastructure/
  MiniAdmin.Shared/
```

## 2. 从零创建命令

如果从一个空目录开始，可以按下面的方式创建同样的项目结构。

### 2.1 创建目录和解决方案

```powershell
mkdir C:\monica\code\mini-admin
cd C:\monica\code\mini-admin

dotnet new sln --format slnx -n MiniAdmin
```

生成结果：

```text
MiniAdmin.slnx
```

### 2.2 创建各层项目

```powershell
mkdir src

dotnet new webapi   -n MiniAdmin.Api                   -o src/MiniAdmin.Api                   -f net10.0
dotnet new classlib -n MiniAdmin.Application           -o src/MiniAdmin.Application           -f net10.0
dotnet new classlib -n MiniAdmin.Application.Contracts -o src/MiniAdmin.Application.Contracts -f net10.0
dotnet new classlib -n MiniAdmin.Domain                -o src/MiniAdmin.Domain                -f net10.0
dotnet new classlib -n MiniAdmin.Domain.Shared         -o src/MiniAdmin.Domain.Shared         -f net10.0
dotnet new classlib -n MiniAdmin.Infrastructure        -o src/MiniAdmin.Infrastructure        -f net10.0
dotnet new classlib -n MiniAdmin.Shared                -o src/MiniAdmin.Shared                -f net10.0
```

### 2.3 把项目加入解决方案

```powershell
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Api/MiniAdmin.Api.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Application/MiniAdmin.Application.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Domain/MiniAdmin.Domain.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Domain.Shared/MiniAdmin.Domain.Shared.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj
dotnet sln MiniAdmin.slnx add src/MiniAdmin.Shared/MiniAdmin.Shared.csproj
```

### 2.4 添加项目引用

```powershell
dotnet add src/MiniAdmin.Api/MiniAdmin.Api.csproj reference src/MiniAdmin.Application/MiniAdmin.Application.csproj
dotnet add src/MiniAdmin.Api/MiniAdmin.Api.csproj reference src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj
dotnet add src/MiniAdmin.Api/MiniAdmin.Api.csproj reference src/MiniAdmin.Shared/MiniAdmin.Shared.csproj

dotnet add src/MiniAdmin.Application/MiniAdmin.Application.csproj reference src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj
dotnet add src/MiniAdmin.Application/MiniAdmin.Application.csproj reference src/MiniAdmin.Domain/MiniAdmin.Domain.csproj
dotnet add src/MiniAdmin.Application/MiniAdmin.Application.csproj reference src/MiniAdmin.Shared/MiniAdmin.Shared.csproj

dotnet add src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj reference src/MiniAdmin.Domain.Shared/MiniAdmin.Domain.Shared.csproj
dotnet add src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj reference src/MiniAdmin.Shared/MiniAdmin.Shared.csproj

dotnet add src/MiniAdmin.Domain/MiniAdmin.Domain.csproj reference src/MiniAdmin.Domain.Shared/MiniAdmin.Domain.Shared.csproj
dotnet add src/MiniAdmin.Domain/MiniAdmin.Domain.csproj reference src/MiniAdmin.Shared/MiniAdmin.Shared.csproj

dotnet add src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj reference src/MiniAdmin.Domain/MiniAdmin.Domain.csproj
dotnet add src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj reference src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj
dotnet add src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj reference src/MiniAdmin.Shared/MiniAdmin.Shared.csproj
```

## 3. `.slnx` 结构说明

当前 `MiniAdmin.slnx` 是 XML 风格的解决方案文件，内容比传统 `.sln` 更容易阅读。

当前结构大致如下：

```xml
<Solution>
  <Folder Name="/src/">
    <Project Path="src/MiniAdmin.Api/MiniAdmin.Api.csproj" />
    <Project Path="src/MiniAdmin.Application.Contracts/MiniAdmin.Application.Contracts.csproj" />
    <Project Path="src/MiniAdmin.Application/MiniAdmin.Application.csproj" />
    <Project Path="src/MiniAdmin.Domain.Shared/MiniAdmin.Domain.Shared.csproj" />
    <Project Path="src/MiniAdmin.Domain/MiniAdmin.Domain.csproj" />
    <Project Path="src/MiniAdmin.Infrastructure/MiniAdmin.Infrastructure.csproj" />
    <Project Path="src/MiniAdmin.Shared/MiniAdmin.Shared.csproj" />
  </Folder>
</Solution>
```

这里的关键点是：

- `Solution` 表示一个解决方案。
- `Folder Name="/src/"` 表示解决方案视图中有一个 `src` 分组。
- 每个 `Project` 指向一个实际的 `.csproj` 文件。
- `.slnx` 只负责组织项目，不负责决定项目之间的依赖关系。
- 项目之间的依赖关系由各自 `.csproj` 里的 `ProjectReference` 决定。

## 4. 每层职责

### 4.1 MiniAdmin.Api

API 层是系统入口，负责接收 HTTP 请求并返回 HTTP 响应。

典型内容：

- `Program.cs`
- Controllers 或 Minimal API Endpoints
- Swagger/OpenAPI 配置
- 认证、授权、中间件注册
- 依赖注入入口

它应该做的事情：

- 接收请求参数。
- 调用 Application 层完成业务用例。
- 把结果转换成 HTTP 响应。

它不应该做的事情：

- 不直接写复杂业务规则。
- 不直接操作数据库细节。
- 不把领域模型和持久化实现混在一起。

### 4.2 MiniAdmin.Application

Application 层是用例编排层，负责组织一次业务操作的流程。

典型内容：

- 应用服务
- 命令处理器
- 查询处理器
- DTO 转换
- 事务边界编排

它应该做的事情：

- 表达“创建用户”“修改角色”“查询菜单”这类应用用例。
- 调用 Domain 层完成核心规则。
- 通过 Contracts 中定义的接口访问外部能力。

它不应该做的事情：

- 不依赖具体数据库实现。
- 不写 HTTP 细节。
- 不把业务规则全部堆在服务方法里。

### 4.3 MiniAdmin.Application.Contracts

Application.Contracts 层放应用层对外暴露的契约，以及应用层需要的抽象。

典型内容：

- 请求 DTO
- 响应 DTO
- 应用服务接口
- 仓储接口
- 当前用户、缓存、消息队列等抽象接口

它的作用是让上层和基础设施层都能围绕接口协作：

- API 层可以依赖应用服务接口。
- Infrastructure 层可以实现仓储、缓存、文件存储等接口。
- Application 层可以面向抽象编排业务流程。

### 4.4 MiniAdmin.Domain

Domain 层是领域核心，负责表达业务规则和业务概念。

典型内容：

- 实体
- 聚合根
- 值对象
- 领域服务
- 领域事件
- 业务规则方法

它应该尽量保持纯粹：

- 不依赖 Web。
- 不依赖数据库。
- 不依赖 EF Core、Redis、消息队列等技术实现。

Domain 是最值得保护的一层，因为业务真正的长期价值通常在这里。

### 4.5 MiniAdmin.Domain.Shared

Domain.Shared 层放领域层和其他层都需要知道的共享领域概念。

典型内容：

- 枚举
- 常量
- 领域错误码
- 轻量级共享类型

它和 `Domain` 的区别是：

- `Domain` 放完整业务模型和规则。
- `Domain.Shared` 放可以安全共享给外部层使用的领域基础类型。

例如，用户状态枚举、菜单类型枚举、通用业务错误码，就适合放在这里。

### 4.6 MiniAdmin.Infrastructure

Infrastructure 层是基础设施实现层，负责对接真实外部资源。

典型内容：

- EF Core DbContext
- Repository 实现
- 数据库迁移
- Redis 实现
- 文件存储实现
- 邮件、短信、第三方 API 实现
- UnitOfWork 实现

它应该做的事情：

- 实现 Application.Contracts 中定义的接口。
- 把数据库、缓存、消息队列等技术细节隔离在这一层。
- 通过依赖注入把实现注册给上层使用。

它不应该做的事情：

- 不把技术细节泄露到 Domain。
- 不让 Application 直接依赖具体外部 SDK。

### 4.7 MiniAdmin.Shared

Shared 层放跨层通用能力，但要保持克制。

典型内容：

- 通用工具类
- 通用结果类型
- 通用异常类型
- 分页模型
- 时间、字符串、编码等基础帮助方法

注意：`Shared` 很容易变成“什么都往里放”的杂物间。使用时要问自己一句：

这个类型是否真的不属于任何明确业务层？

如果它是业务概念，优先考虑放到 `Domain` 或 `Domain.Shared`；如果它是应用契约，优先考虑放到 `Application.Contracts`。

## 5. 引用方向

当前项目引用关系如下：

```text
MiniAdmin.Api
  -> MiniAdmin.Application
  -> MiniAdmin.Infrastructure
  -> MiniAdmin.Shared

MiniAdmin.Application
  -> MiniAdmin.Application.Contracts
  -> MiniAdmin.Domain
  -> MiniAdmin.Shared

MiniAdmin.Application.Contracts
  -> MiniAdmin.Domain.Shared
  -> MiniAdmin.Shared

MiniAdmin.Domain
  -> MiniAdmin.Domain.Shared
  -> MiniAdmin.Shared

MiniAdmin.Infrastructure
  -> MiniAdmin.Domain
  -> MiniAdmin.Application.Contracts
  -> MiniAdmin.Shared

MiniAdmin.Domain.Shared
  -> no project reference

MiniAdmin.Shared
  -> no project reference
```

可以把它理解成：

```text
Api
  -> Application
  -> Infrastructure

Application
  -> Contracts
  -> Domain

Infrastructure
  -> Contracts
  -> Domain

Domain
  -> Domain.Shared

Most layers
  -> Shared
```

更重要的是反过来看：

- `Domain` 不引用 `Application`。
- `Domain` 不引用 `Infrastructure`。
- `Application.Contracts` 不引用 `Infrastructure`。
- `Infrastructure` 可以实现接口，但接口本身不应该定义在 `Infrastructure`。

这能避免核心业务被数据库、Web 框架或外部 SDK 绑死。

## 6. 为什么这样分层

这个结构的核心目标不是“文件夹看起来高级”，而是为了控制变化。

### 6.1 业务规则更稳定

后台管理系统会经常换接口形式、数据库实现、权限方案、缓存方案，但核心业务规则往往更稳定。

把业务规则放在 `Domain`，可以减少技术变化对核心逻辑的影响。

### 6.2 用例流程更清楚

`Application` 负责把一次操作串起来：

```text
校验请求 -> 读取数据 -> 调用领域规则 -> 保存结果 -> 返回 DTO
```

这样 API 层不会越来越胖，领域层也不会被迫知道太多应用流程。

### 6.3 技术实现可替换

`Infrastructure` 实现接口，`Application` 面向接口工作。

以后如果数据库从 SQL Server 换成 PostgreSQL，或者缓存从内存换成 Redis，理想情况下主要修改 Infrastructure，而不是全项目到处改。

### 6.4 测试更容易

当 Application 依赖抽象接口时，测试可以用假的仓储、假的当前用户、假的时间服务。

这会让单元测试更轻，不必每个测试都启动真实数据库或真实 Web 服务。

## 7. 下一步学习路线

建议按下面顺序继续推进。

### 第一步：清理模板代码

处理内容：

- 删除各 class library 中默认生成的 `Class1.cs`。
- 检查 `MiniAdmin.Api` 中的默认 WeatherForecast 示例。
- 保留最小可运行 API。

学习目标：

- 熟悉每个项目的目录。
- 确认解决方案能正常编译。

验证命令：

```powershell
dotnet build MiniAdmin.slnx
```

### 第二步：定义统一返回模型

建议放在：

```text
src/MiniAdmin.Shared
```

可以先设计：

- `Result`
- `Result<T>`
- `PagedResult<T>`

学习目标：

- 理解跨层通用类型应该如何设计。
- 避免 Controller 每次手写不同响应格式。

### 第三步：建立第一个领域模型

可以从后台系统最常见的用户模型开始。

建议放在：

```text
src/MiniAdmin.Domain
```

可以先设计：

- `User`
- `Role`
- `Permission`

学习目标：

- 区分实体和值对象。
- 把业务规则写到领域对象里，而不是 Controller 里。

### 第四步：定义应用契约

建议放在：

```text
src/MiniAdmin.Application.Contracts
```

可以先设计：

- `IUserAppService`
- `CreateUserRequest`
- `UserDto`
- `IUserRepository`

学习目标：

- 理解 DTO 和 Entity 的区别。
- 理解接口为什么放在 Contracts。

### 第五步：实现应用服务

建议放在：

```text
src/MiniAdmin.Application
```

可以先实现：

- 创建用户
- 查询用户列表
- 修改用户状态

学习目标：

- 理解 Application 层如何编排用例。
- 学会把业务规则交给 Domain，把外部访问交给接口。

### 第六步：接入基础设施

建议放在：

```text
src/MiniAdmin.Infrastructure
```

可以逐步加入：

- EF Core
- DbContext
- Repository 实现
- 数据库迁移

学习目标：

- 理解 Infrastructure 如何实现 Contracts 中的接口。
- 学会把数据库细节隔离在基础设施层。

### 第七步：连接 API

建议放在：

```text
src/MiniAdmin.Api
```

可以先做：

- 注册依赖注入
- 创建用户接口
- 查询用户接口
- Swagger 调试

学习目标：

- 理解 API 层只负责 HTTP 入口。
- 学会从请求一路走到 Application、Domain、Infrastructure。

## 8. 当前阶段最重要的原则

现阶段不要急着把功能做复杂。更重要的是保持每一层职责清楚：

- API 负责入口。
- Application 负责编排。
- Contracts 负责契约。
- Domain 负责业务规则。
- Domain.Shared 负责共享领域基础类型。
- Infrastructure 负责技术实现。
- Shared 负责真正通用的基础能力。

只要这个边界先立住，后面加用户、角色、权限、菜单、日志、审计、租户、认证授权时，项目就不会很快失控。
