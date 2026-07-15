namespace MiniAdmin.Platform.DynamicApi;

public enum DynamicApiParameterSource
{
    Auto,
    Route,
    Query,
    Body,
    Header,
    Services
}

[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
public sealed class DynamicApiParameterAttribute(
    DynamicApiParameterSource source,
    string? name = null) : Attribute
{
    public DynamicApiParameterSource Source { get; } = source;

    public string? Name { get; } = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
}
