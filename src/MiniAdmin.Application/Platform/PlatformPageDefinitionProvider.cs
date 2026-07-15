using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.Platform;

public sealed class PlatformPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "platform.kernel",
            ParentKey: null,
            Path: "/platform-kernel",
            Component: null,
            Redirect: "/platform-kernel/access-control",
            Icon: "lucide:blocks",
            Order: 900,
            I18nKey: "page.platform.title",
            Title: new LocalizedText("平台内核", "Platform Kernel"),
            IsVisible: true,
            Permissions:
            [
                new PermissionDefinition(
                    "platform:metadata:query",
                    "platform.metadata",
                    "query",
                    "permission.platform.metadata.query",
                    new LocalizedText("查看平台元数据", "View platform metadata"))
            ]);

        yield return new PageDefinition(
            Key: "platform.access-control",
            ParentKey: "platform.kernel",
            Path: "/platform-kernel/access-control",
            Component: "/platform/access-control/index",
            Redirect: null,
            Icon: "lucide:shield-check",
            Order: 10,
            I18nKey: "page.platform.accessControl.title",
            Title: new LocalizedText("访问控制策略", "Access Control Policies"),
            IsVisible: true,
            Permissions:
            [
                new PermissionDefinition(
                    "platform:abac:query",
                    "platform.abac-policy",
                    "query",
                    "permission.platform.abac.query",
                    new LocalizedText("查询 ABAC 策略", "Query ABAC policies")),
                new PermissionDefinition(
                    "platform:abac:create",
                    "platform.abac-policy",
                    "create",
                    "permission.platform.abac.create",
                    new LocalizedText("创建 ABAC 策略", "Create ABAC policies")),
                new PermissionDefinition(
                    "platform:abac:update",
                    "platform.abac-policy",
                    "update",
                    "permission.platform.abac.update",
                    new LocalizedText("更新 ABAC 策略", "Update ABAC policies")),
                new PermissionDefinition(
                    "platform:abac:delete",
                    "platform.abac-policy",
                    "delete",
                    "permission.platform.abac.delete",
                    new LocalizedText("删除 ABAC 策略", "Delete ABAC policies"))
            ]);

        yield return new PageDefinition(
            Key: "platform.cache",
            ParentKey: "platform.kernel",
            Path: "/platform-kernel/cache",
            Component: "/platform/cache/index",
            Redirect: null,
            Icon: "lucide:database-zap",
            Order: 20,
            I18nKey: "page.platform.cache.title",
            Title: new LocalizedText("缓存管理", "Cache Management"),
            IsVisible: true,
            Permissions:
            [
                new PermissionDefinition(
                    "platform:cache:query",
                    "platform.cache",
                    "query",
                    "permission.platform.cache.query",
                    new LocalizedText("查询平台缓存", "Query platform cache")),
                new PermissionDefinition(
                    "platform:cache:clear",
                    "platform.cache",
                    "clear",
                    "permission.platform.cache.clear",
                    new LocalizedText("清理平台缓存", "Clear platform cache"))
            ]);
    }
}
