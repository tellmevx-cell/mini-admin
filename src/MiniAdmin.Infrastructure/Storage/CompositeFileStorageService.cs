using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

public sealed class CompositeFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService
{
    private readonly LocalFileStorageService localStorage = new(options);
    private readonly MinioFileStorageService minioStorage = new(options);

    public string ProviderName => GetCurrentProvider().ProviderName;

    public Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        return GetCurrentProvider().SaveAsync(content, originalName, contentType, cancellationToken);
    }

    public Task<Stream> OpenReadAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        return GetProvider(storageProvider).OpenReadAsync(storagePath, cancellationToken);
    }

    public Task<bool> ExistsAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        return GetProvider(storageProvider).ExistsAsync(storagePath, cancellationToken);
    }

    public Task DeleteAsync(
        string storageProvider,
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        return GetProvider(storageProvider).DeleteAsync(storagePath, cancellationToken);
    }

    private IFileStorageProvider GetCurrentProvider()
    {
        return GetProvider(options.Value.Provider);
    }

    private IFileStorageProvider GetProvider(string storageProvider)
    {
        if (storageProvider.Equals("Minio", StringComparison.OrdinalIgnoreCase) ||
            storageProvider.Equals("minio", StringComparison.OrdinalIgnoreCase))
        {
            return minioStorage;
        }

        if (storageProvider.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
            storageProvider.Equals("local", StringComparison.OrdinalIgnoreCase))
        {
            return localStorage;
        }

        throw new InvalidOperationException($"Unsupported file storage provider: {storageProvider}.");
    }
}
