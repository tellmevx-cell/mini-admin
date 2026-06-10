namespace MiniAdmin.Domain.Entities;

public sealed class DataSeedVersion
{
    public Guid Id { get; set; }

    public string Version { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public DateTimeOffset AppliedAt { get; set; }
}
