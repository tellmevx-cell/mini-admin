namespace MiniAdmin.Domain.Entities;

public sealed class ManagedFile
{
    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public string OriginalName { get; set; } = string.Empty;

    public string StoredName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string StorageProvider { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string Status { get; set; } = "Normal";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
