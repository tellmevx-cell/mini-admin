namespace MiniAdmin.Platform.Navigation;

public sealed class PageRegistry : IPageRegistry
{
    private readonly IReadOnlyDictionary<string, PageDefinition> pagesByKey;
    private readonly IReadOnlyDictionary<string, PermissionDefinition> permissionsByCode;

    public PageRegistry(IEnumerable<IPageDefinitionProvider> providers)
    {
        var pages = providers
            .SelectMany(provider => provider.GetPages())
            .OrderBy(page => page.Order)
            .ThenBy(page => page.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        pagesByKey = BuildUniqueMap(pages, page => page.Key, "page key");
        permissionsByCode = BuildUniqueMap(
            pages.SelectMany(page => page.Permissions),
            permission => permission.Code,
            "permission code");
        ValidateParents(pages);

        Pages = pages;
    }

    public IReadOnlyList<PageDefinition> Pages { get; }

    public PageDefinition? FindPage(string key)
    {
        return pagesByKey.GetValueOrDefault(key);
    }

    public PermissionDefinition? FindPermission(string code)
    {
        return permissionsByCode.GetValueOrDefault(code);
    }

    private static IReadOnlyDictionary<string, T> BuildUniqueMap<T>(
        IEnumerable<T> items,
        Func<T, string> keySelector,
        string keyName)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            var key = keySelector(item);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException($"Page registry {keyName} cannot be empty.");
            }

            if (!result.TryAdd(key, item))
            {
                throw new InvalidOperationException($"Duplicate page registry {keyName}: {key}.");
            }
        }

        return result;
    }

    private void ValidateParents(IEnumerable<PageDefinition> pages)
    {
        foreach (var page in pages)
        {
            if (string.IsNullOrWhiteSpace(page.ParentKey))
            {
                continue;
            }

            if (!pagesByKey.ContainsKey(page.ParentKey))
            {
                throw new InvalidOperationException(
                    $"Page '{page.Key}' references missing parent '{page.ParentKey}'.");
            }

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { page.Key };
            var current = page;
            while (!string.IsNullOrWhiteSpace(current.ParentKey))
            {
                if (!visited.Add(current.ParentKey))
                {
                    throw new InvalidOperationException($"Page registry contains a cycle at '{page.Key}'.");
                }

                current = pagesByKey[current.ParentKey];
            }
        }
    }
}
