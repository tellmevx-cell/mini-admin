namespace MiniAdmin.Domain.Entities;

public sealed class DictionaryType
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool IsEnabled { get; set; } = true;

    public List<DictionaryItem> Items { get; set; } = [];
}
