namespace MiniAdmin.Application.Contracts.Files;

public sealed record FileListQuery(
    int Page = 1,
    int PageSize = 20,
    string? OriginalName = null,
    string? StorageProvider = null);
