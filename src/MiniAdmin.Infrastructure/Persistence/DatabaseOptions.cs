namespace MiniAdmin.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public string Provider { get; set; } = "InMemory";

    public bool InitializeOnStartup { get; set; } = true;

    public string SchemaManagement { get; set; } = "Auto";

    public string InMemoryDatabaseName { get; set; } = "MiniAdmin";

    public string MySqlServerVersion { get; set; } = "8.0.36-mysql";
}
