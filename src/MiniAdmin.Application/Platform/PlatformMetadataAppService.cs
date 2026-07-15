using MiniAdmin.Platform.DynamicApi;
using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.Platform;

[DynamicApi("platform/metadata", Name = "PlatformMetadata", Tag = "平台元数据")]
public sealed class PlatformMetadataAppService(IPageRegistry pageRegistry)
{
    [DynamicGet(
        "pages",
        Permission = "platform:metadata:query",
        Resource = "platform.metadata",
        Action = "query",
        OperationId = "GetPlatformPages",
        Summary = "获取后端 PageRegistry 页面快照")]
    public Task<IReadOnlyList<PageDefinition>> GetPagesAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(pageRegistry.Pages);
    }

    [DynamicGet(
        "permissions",
        Permission = "platform:metadata:query",
        Resource = "platform.metadata",
        Action = "query",
        OperationId = "GetPlatformPermissions",
        Summary = "获取 PageRegistry 权限元数据")]
    public Task<IReadOnlyList<PermissionDefinition>> GetPermissionsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PermissionDefinition> permissions = pageRegistry.Pages
            .SelectMany(page => page.Permissions)
            .OrderBy(permission => permission.Code, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Task.FromResult(permissions);
    }
}
