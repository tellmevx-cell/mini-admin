namespace MiniAdmin.Application.Contracts.Files;

public sealed record CreateFileRecordRequest(
    string OriginalName,
    string StoredName,
    string ContentType,
    long Size,
    string StorageProvider,
    string StoragePath,
    string Status,
    DateTimeOffset CreatedAt);
