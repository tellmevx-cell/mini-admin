using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

public sealed class CompositeFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageService, IDisposable
{
    private readonly LocalFileStorageService localStorage = new(options);
    private readonly Dictionary<string, S3CompatibleFileStorageService> objectStorages =
        new(StringComparer.OrdinalIgnoreCase);

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

    public void Dispose()
    {
        foreach (var provider in objectStorages.Values)
        {
            provider.Dispose();
        }

        objectStorages.Clear();
    }

    private IFileStorageProvider GetCurrentProvider()
    {
        return GetProvider(options.Value.Provider);
    }

    private IFileStorageProvider GetProvider(string storageProvider)
    {
        var normalized = storageProvider.Trim().ToLowerInvariant();
        if (normalized is "minio" or "s3" or "oss" or "cos")
        {
            if (objectStorages.TryGetValue(normalized, out var existing))
            {
                return existing;
            }

            S3CompatibleStorageOptions providerOptions = normalized switch
            {
                "minio" => options.Value.Minio,
                "s3" => options.Value.S3,
                "oss" => options.Value.Oss,
                "cos" => options.Value.Cos,
                _ => throw new InvalidOperationException($"Unsupported file storage provider: {storageProvider}.")
            };
            var created = new S3CompatibleFileStorageService(normalized, providerOptions);
            objectStorages[normalized] = created;
            return created;
        }

        if (normalized == "local")
        {
            return localStorage;
        }

        throw new InvalidOperationException($"Unsupported file storage provider: {storageProvider}.");
    }
}
