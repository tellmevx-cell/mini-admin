using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Platform.DynamicApi;
using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.OpenPlatform;

[DynamicApi("open-platform/applications", Name = "OpenPlatformApplications", Tag = "开放平台")]
public sealed class OpenPlatformApplicationAppService(
    IOpenPlatformApplicationRepository repository,
    IPageRegistry pageRegistry) : IOpenPlatformApplicationAppService
{
    [DynamicGet(
        Permission = "open-platform:application:query",
        Resource = "open-platform.application",
        Action = "query",
        OperationId = "GetOpenPlatformApplications",
        Summary = "查询第三方应用")]
    public Task<IReadOnlyList<OpenPlatformApplicationDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        return repository.GetListAsync(cancellationToken);
    }

    [DynamicPost(
        Permission = "open-platform:application:create",
        Resource = "open-platform.application",
        Action = "create",
        OperationId = "CreateOpenPlatformApplication",
        Summary = "注册第三方应用")]
    public Task<OpenPlatformApplicationSecretDto> CreateAsync(
        CreateOpenPlatformApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);
        return repository.CreateAsync(request, cancellationToken);
    }

    [DynamicPost(
        "{id:guid}/rotate-secret",
        Permission = "open-platform:application:rotate-secret",
        Resource = "open-platform.application",
        Action = "rotate-secret",
        OperationId = "RotateOpenPlatformApplicationSecret",
        Summary = "轮换第三方应用密钥")]
    public Task<string?> RotateSecretAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid id,
        CancellationToken cancellationToken = default)
    {
        return repository.RotateSecretAsync(id, cancellationToken);
    }

    [DynamicDelete(
        "{id:guid}",
        Permission = "open-platform:application:delete",
        Resource = "open-platform.application",
        Action = "delete",
        OperationId = "DeleteOpenPlatformApplication",
        Summary = "删除第三方应用并撤销令牌")]
    public Task<bool> DeleteAsync(
        [DynamicApiParameter(DynamicApiParameterSource.Route)] Guid id,
        CancellationToken cancellationToken = default)
    {
        return repository.DeleteAsync(id, cancellationToken);
    }

    private void Validate(CreateOpenPlatformApplicationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) || request.DisplayName.Trim().Length > 128)
        {
            throw new InvalidOperationException("应用名称不能为空且不能超过 128 个字符。");
        }

        if (request.ClientType is not ("Public" or "Confidential"))
        {
            throw new InvalidOperationException("客户端类型只支持 Public 或 Confidential。");
        }

        if (request.AllowClientCredentials && request.ClientType != "Confidential")
        {
            throw new InvalidOperationException("只有 Confidential 客户端可以使用客户端凭证模式。");
        }

        foreach (var value in request.RedirectUris.Concat(request.PostLogoutRedirectUris ?? []))
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
            {
                throw new InvalidOperationException("重定向地址必须是 HTTPS 绝对地址；仅本机地址允许 HTTP。");
            }
        }

        var knownPermissions = pageRegistry.Pages
            .SelectMany(page => page.Permissions)
            .Select(permission => permission.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknownPermissions = (request.ApiPermissions ?? [])
            .Where(permission => !knownPermissions.Contains(permission))
            .ToArray();
        if (unknownPermissions.Length > 0)
        {
            throw new InvalidOperationException(
                $"存在未在 PageRegistry 注册的权限码：{string.Join(", ", unknownPermissions)}");
        }
    }
}
