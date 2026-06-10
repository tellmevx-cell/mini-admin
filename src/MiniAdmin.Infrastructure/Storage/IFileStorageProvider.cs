using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

internal interface IFileStorageProvider
{
    string ProviderName { get; }

    Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default);
}
