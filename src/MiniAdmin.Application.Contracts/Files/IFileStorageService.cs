namespace MiniAdmin.Application.Contracts.Files;

public interface IFileStorageService
{
    string ProviderName { get; }

    Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default);
}
