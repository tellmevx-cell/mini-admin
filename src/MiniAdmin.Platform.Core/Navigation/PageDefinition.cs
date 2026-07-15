namespace MiniAdmin.Platform.Navigation;

public sealed record LocalizedText(string ZhCn, string EnUs)
{
    public string Resolve(string? cultureName)
    {
        return cultureName?.StartsWith("en", StringComparison.OrdinalIgnoreCase) == true
            ? EnUs
            : ZhCn;
    }
}

public sealed record PermissionDefinition(
    string Code,
    string Resource,
    string Action,
    string I18nKey,
    LocalizedText Title,
    Guid? Id = null);

public sealed record PageDefinition(
    string Key,
    string? ParentKey,
    string Path,
    string? Component,
    string? Redirect,
    string Icon,
    int Order,
    string I18nKey,
    LocalizedText Title,
    bool IsVisible,
    IReadOnlyList<PermissionDefinition> Permissions,
    Guid? Id = null,
    Guid? ParentId = null);

public interface IPageDefinitionProvider
{
    IEnumerable<PageDefinition> GetPages();
}

public interface IPageRegistry
{
    IReadOnlyList<PageDefinition> Pages { get; }

    PageDefinition? FindPage(string key);

    PermissionDefinition? FindPermission(string code);
}
