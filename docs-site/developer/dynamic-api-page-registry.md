# Dynamic API 与 PageRegistry

新业务模块不再手写标准 CRUD Controller，也不再创建菜单种子。应用服务是接口事实源，PageRegistry 是页面、路由、组件和权限事实源。

## 定义应用服务

```csharp
using MiniAdmin.Platform.DynamicApi;

[DynamicApi("business/product", Name = "Product", Tag = "Business")]
public sealed class ProductAppService(IProductRepository repository)
{
    [DynamicGet(
        "list",
        Permission = "business:product:query",
        Resource = "business.product",
        Action = "query",
        OperationId = "Product_GetList")]
    public Task<PageResult<ProductDto>> GetListAsync(
        ProductListQuery query,
        CancellationToken cancellationToken = default)
        => repository.GetListAsync(query, cancellationToken);

    [DynamicPost(
        Permission = "business:product:create",
        Resource = "business.product",
        Action = "create")]
    public Task<ProductDto> CreateAsync(
        SaveProductRequest request,
        CancellationToken cancellationToken = default)
        => repository.CreateAsync(request, cancellationToken);
}
```

规则：

- 类上 route 不带前导 `/`。
- 只有标记了 Dynamic 方法特性的公开方法会暴露。
- 路由参数使用 `{id:guid}`，复杂 GET 对象自动绑定 Query，首个复杂 POST/PUT 对象绑定 Body。
- 框架服务参数使用 `[DynamicApiParameter(DynamicApiParameterSource.Services)]`。
- `Permission` 用于 RBAC，`Resource + Action` 用于 ABAC，两者必须和 PageRegistry 一致。

Dynamic API 返回 DTO 原始 JSON，不套旧 `ApiResponse`。前端请求应指定：

```ts
requestClient.get<ProductListResult>('/business/product/list', {
  params,
  responseReturn: 'body',
});
```

## 注册页面与权限

```csharp
using MiniAdmin.Platform.Navigation;

public sealed class ProductPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "business.product",
            ParentKey: null,
            Path: "/business/product",
            Component: "/business/product/index",
            Redirect: null,
            Icon: "lucide:package",
            Order: 100,
            I18nKey: "page.business.product.title",
            Title: new LocalizedText("产品管理", "Products"),
            IsVisible: true,
            Permissions:
            [
                new PermissionDefinition(
                    "business:product:query",
                    "business.product",
                    "query",
                    "permission.business.product.query",
                    new LocalizedText("查询产品", "Query products"))
            ]);
    }
}
```

Provider 会从 Application 程序集自动发现。启动时同步到菜单表并给平台管理员授权；代码中的路径、组件、顺序和可见性是事实源。`ParentKey` 用于挂到另一个注册页面，`ParentId` 可挂到历史数据库菜单。

## 文件传输例外

文件上传、下载需要 `IFormFile`、流和 `Results.File`，属于 HTTP 传输细节。代码生成器仅在启用导入导出时生成 `*TransportEndpoints.cs` 薄适配器，标准查询和写操作仍全部由 Dynamic API 暴露。

## 代码生成器

生成器现在产出：

- 分层实体、DTO、接口、应用服务、仓储和 EF 配置。
- 带 RBAC/ABAC 元数据的 Dynamic API 应用服务。
- `PageDefinitionProvider`，不再生成 `MenuSeed`。
- 前端 API 和页面。
- 可选工作流状态处理器与导入导出传输适配器。

系统维护表（包括 `__EFMigrationsHistory`）会被生成器拒绝，避免误生成可编辑业务模块。

## 验收

```powershell
dotnet build MiniAdmin.slnx -c Release
dotnet test tests/MiniAdmin.Tests/MiniAdmin.Tests.csproj -c Release
```

运行后还应检查 `/openapi/v1.json` 是否包含 OperationId，并在 Scalar 中验证请求模型和授权结果。
