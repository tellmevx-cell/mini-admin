using MiniAdmin.Platform.Navigation;

namespace MiniAdmin.Application.OpenPlatform;

public sealed class OpenPlatformPageDefinitionProvider : IPageDefinitionProvider
{
    public IEnumerable<PageDefinition> GetPages()
    {
        yield return new PageDefinition(
            Key: "open-platform.applications",
            ParentKey: null,
            Path: "/open-platform/applications",
            Component: "/open-platform/applications/index",
            Redirect: null,
            Icon: "lucide:key-round",
            Order: 910,
            I18nKey: "page.openPlatform.applications.title",
            Title: new LocalizedText("开放平台应用", "Open Platform Applications"),
            IsVisible: true,
            Permissions:
            [
                Permission("query", "查看第三方应用", "View third-party applications"),
                Permission("create", "注册第三方应用", "Register third-party applications"),
                Permission("rotate-secret", "轮换应用密钥", "Rotate application secrets"),
                Permission("delete", "删除第三方应用", "Delete third-party applications"),
                new PermissionDefinition(
                    "open-platform:credential:manage",
                    "open-platform.credential",
                    "manage",
                    "permission.openPlatform.credential.manage",
                    new LocalizedText("管理个人 OpenAPI 凭证", "Manage personal OpenAPI credentials"))
            ]);
    }

    private static PermissionDefinition Permission(
        string action,
        string zhCn,
        string enUs)
    {
        return new PermissionDefinition(
            $"open-platform:application:{action}",
            "open-platform.application",
            action,
            $"permission.openPlatform.application.{action}",
            new LocalizedText(zhCn, enUs));
    }
}
