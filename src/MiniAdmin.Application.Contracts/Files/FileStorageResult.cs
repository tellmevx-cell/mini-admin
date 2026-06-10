namespace MiniAdmin.Application.Contracts.Files;

public sealed record FileStorageResult(
    string StorageProvider,
    string StoragePath,
    string StoredName);
