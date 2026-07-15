namespace MiniAdmin.Platform.DynamicApi;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class DynamicApiAttribute(string route) : Attribute
{
    public string Route { get; } = NormalizeRoute(route);

    public string? Name { get; init; }

    public string? Tag { get; init; }

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            throw new ArgumentException("Dynamic API route cannot be empty.", nameof(route));
        }

        return route.Trim().Trim('/');
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class DynamicApiMethodAttribute(string httpMethod, string route = "") : Attribute
{
    public string HttpMethod { get; } = NormalizeHttpMethod(httpMethod);

    public string Route { get; } = route.Trim().Trim('/');

    public bool AllowAnonymous { get; init; }

    public string? Permission { get; init; }

    public string? Resource { get; init; }

    public string? Action { get; init; }

    public string? OperationId { get; init; }

    public string? Summary { get; init; }

    private static string NormalizeHttpMethod(string httpMethod)
    {
        if (string.IsNullOrWhiteSpace(httpMethod))
        {
            throw new ArgumentException("Dynamic API HTTP method cannot be empty.", nameof(httpMethod));
        }

        return httpMethod.Trim().ToUpperInvariant();
    }
}

public sealed class DynamicGetAttribute(string route = "") : DynamicApiMethodAttribute("GET", route);

public sealed class DynamicPostAttribute(string route = "") : DynamicApiMethodAttribute("POST", route);

public sealed class DynamicPutAttribute(string route = "") : DynamicApiMethodAttribute("PUT", route);

public sealed class DynamicDeleteAttribute(string route = "") : DynamicApiMethodAttribute("DELETE", route);
