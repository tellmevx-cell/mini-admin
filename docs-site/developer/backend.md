# 后端开发

本页说明新增或修改后端能力时的常规做法。

## 新增后端能力的顺序

1. 在 Domain 定义实体或领域类型。
2. 在 Application.Contracts 定义 DTO、请求对象和接口。
3. 在 Application 实现应用服务。
4. 在 Infrastructure 实现仓储、存储或外部服务。
5. 用 Dynamic API 元数据暴露标准接口；仅为文件流增加传输端点。
6. 用 PageRegistry 注册页面、路由、组件、权限和国际化。
7. 增加测试。

## DTO 和请求对象

DTO 放在：

```text
src/MiniAdmin.Application.Contracts
```

建议：

- 请求对象和响应对象分开。
- 不把 EF 实体直接暴露给前端。
- 列表查询统一包含分页、关键词和筛选字段。
- 写操作请求对象只包含用户可提交字段。

## 应用服务

应用服务负责业务编排。

建议：

- 校验当前用户是否有权限操作。
- 查询数据时考虑租户和数据权限。
- 写操作记录必要的状态变化。
- 返回 DTO，不返回实体。
- 复杂逻辑拆成私有方法或独立服务。

## Dynamic API

应用服务使用 `[DynamicApi]` 和 `[DynamicGet/Post/Put/Delete]` 声明：

- 路由定义。
- HTTP 方法与参数绑定。
- RBAC 权限码。
- ABAC 资源和动作。
- OpenAPI OperationId 与说明。

框架在启动时发现并生成 API 与 Scalar 文档。上传、下载等需要 `IFormFile`、流或 `Results.File` 的场景保留薄传输端点，传输层不承载业务逻辑。

## 统一响应

Dynamic API 直接返回 DTO JSON，前端使用 `responseReturn: 'body'`。历史传输端点仍可返回 `ApiResponse<T>`；新增模块不要混用两种契约。业务校验异常统一映射为 `400`，资源不存在为 `404`，越权为 `403`。

完整示例见 [Dynamic API 与 PageRegistry](./dynamic-api-page-registry.md)。

## 测试建议

优先写应用服务测试和关键接口测试。

常用命令：

```powershell
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj --filter YourFeatureTests
```

测试至少覆盖：

- 正常创建或查询。
- 权限或状态不允许。
- 租户或用户边界。
- 关键状态流转。

## 事件和事务

复杂写操作可以注入 `IUnitOfWork`：

```csharp
await unitOfWork.BeginTransactionAsync(cancellationToken);
unitOfWork.AddPostCommitEvent(new SomeLocalEvent(id));
await unitOfWork.CommitAsync(cancellationToken);
```

需要解耦副作用时，定义 `ILocalEvent` 和 `ILocalEventHandler<TEvent>`。提交后通知、清缓存、扩展日志等动作适合放在事件 handler 中。
