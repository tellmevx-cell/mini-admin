using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Platform.DynamicApi;
using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.OpenPlatform;

[DynamicApi("open-platform/credentials", Name = "OpenApiCredentials", Tag = "开放平台")]
public sealed class OpenApiCredentialAppService(
    IOpenApiCredentialRepository repository,
    ICurrentUserContext currentUser,
    IPageRegistry pageRegistry) : IOpenApiCredentialAppService
{
    [DynamicGet(
        "my",
        Permission = "open-platform:credential:manage",
        Resource = "open-platform.credential",
        Action = "query",
        OperationId = "GetMyOpenApiCredentials",
        Summary = "查询我的 OpenAPI 凭证")]
    public Task<IReadOnlyList<OpenApiCredentialDto>> GetMyAsync(
        CancellationToken cancellationToken = default)
    {
        return repository.GetMyAsync(currentUser.UserId, cancellationToken);
    }

    [DynamicPost(
        "my",
        Permission = "open-platform:credential:manage",
        Resource = "open-platform.credential",
        Action = "create",
        OperationId = "CreateMyOpenApiCredential",
        Summary = "创建我的 OpenAPI 凭证，Secret 仅返回一次")]
    public Task<OpenApiCredentialSecretDto> CreateAsync(
        CreateOpenApiCredentialRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return repository.CreateAsync(
            currentUser.UserId,
            currentUser.TenantId,
            request,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    [DynamicDelete(
        "my/{id:guid}",
        Permission = "open-platform:credential:manage",
        Resource = "open-platform.credential",
        Action = "revoke",
        OperationId = "RevokeMyOpenApiCredential",
        Summary = "撤销我的 OpenAPI 凭证")]
    public Task<bool> RevokeAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid id,
        CancellationToken cancellationToken = default)
    {
        return repository.RevokeAsync(
            currentUser.UserId,
            id,
            DateTimeOffset.UtcNow,
            cancellationToken);
    }

    private void Validate(CreateOpenApiCredentialRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 128)
        {
            throw new InvalidOperationException("凭证名称不能为空且不能超过 128 个字符。");
        }

        if (request.ExpiresAt.HasValue && request.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("凭证过期时间必须晚于当前时间。");
        }

        var registered = pageRegistry.Pages
            .SelectMany(page => page.Permissions)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = request.Permissions
            .Where(permission => !registered.Contains(permission))
            .ToArray();
        if (invalid.Length > 0)
        {
            throw new InvalidOperationException(
                $"存在未在 PageRegistry 注册的权限码：{string.Join(", ", invalid)}");
        }
    }
}
