using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Infrastructure.Storage;

internal sealed class LocalFileStorageService(IOptions<FileStorageOptions> options) : IFileStorageProvider
{
    public string ProviderName => "local";

    public async Task<FileStorageResult> SaveAsync(
        Stream content,
        string originalName,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        var (storedName, storagePath) = FileStorageNameHelper.CreateStorageNames(originalName);
        var fullPath = ResolveFullPath(storagePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        return new FileStorageResult(ProviderName, storagePath, storedName);
    }

    public Task<Stream> OpenReadAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        Stream stream = File.OpenRead(fullPath);

        return Task.FromResult(stream);
    }

    public Task<bool> ExistsAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storagePath);

        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteAsync(
        string storagePath,
        CancellationToken cancellationToken = default)
    {
        var fullPath = ResolveFullPath(storagePath);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string ResolveFullPath(string storagePath)
    {
        var rootPath = options.Value.Local.RootPath;
        var rootedPath = Path.IsPathRooted(rootPath)
            ? rootPath
            : Path.Combine(AppContext.BaseDirectory, rootPath);
        var fullRoot = Path.GetFullPath(rootedPath);
        var fullPath = Path.GetFullPath(Path.Combine(
            fullRoot,
            storagePath.Replace('/', Path.DirectorySeparatorChar)));

        if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid storage path.");
        }

        return fullPath;
    }
}
