namespace MiniAdmin.Domain.Entities;

public sealed class DictionaryItem
{
    public Guid Id { get; set; }

    public Guid TypeId { get; set; }

    public DictionaryType Type { get; set; } = null!;

    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Color { get; set; }

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;
}
