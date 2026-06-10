namespace MiniAdmin.Application.Contracts.Files;

public sealed record FileDto(
    string Id,
    string OriginalName,
    string StoredName,
    string ContentType,
    long Size,
    string StorageProvider,
    string StoragePath,
    string Status,
    DateTimeOffset CreatedAt);
