namespace MiniAdmin.Domain.Entities;

public sealed class SystemParameter
{
    public Guid Id { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string Group { get; set; } = string.Empty;

    public string? Remark { get; set; }

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;
}
