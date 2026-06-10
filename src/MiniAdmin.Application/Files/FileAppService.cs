using MiniAdmin.Application.Contracts.Common;
using MiniAdmin.Application.Contracts.Files;

namespace MiniAdmin.Application.Files;

public sealed class FileAppService(
    IFileRepository fileRepository,
    IFileStorageService fileStorageService) : IFileAppService
{
    public Task<PageResult<FileDto>> GetListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default)
    {
        return fileRepository.GetListAsync(query, cancellationToken);
    }

    public async Task<FileDto> UploadAsync(
        Stream content,
        string originalName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(originalName))
        {
            throw new InvalidOperationException("File name is required.");
        }

        var storageResult = await fileStorageService.SaveAsync(
            content,
            originalName,
            string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
            cancellationToken);

        return await fileRepository.CreateAsync(
            new CreateFileRecordRequest(
                OriginalName: Path.GetFileName(originalName),
                StoredName: storageResult.StoredName,
                ContentType: string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType,
                Size: size,
                StorageProvider: storageResult.StorageProvider,
                StoragePath: storageResult.StoragePath,
                Status: "Normal",
                CreatedAt: DateTimeOffset.UtcNow),
            cancellationToken);
    }

    public async Task<FileDownloadResult?> DownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(id, cancellationToken);
        if (file is null)
        {
            return null;
        }

        if (!file.Status.Equals("Normal", StringComparison.OrdinalIgnoreCase))
        {
            throw new FileUnavailableException($"文件当前状态为 {file.Status}，不可下载。");
        }

        var content = await fileStorageService.OpenReadAsync(
            file.StorageProvider,
            file.StoragePath,
            cancellationToken);
        return new FileDownloadResult(file.OriginalName, file.ContentType, content);
    }

    public Task<FileDto?> MarkInvalidAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return fileRepository.MarkInvalidAsync(id, cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var file = await fileRepository.GetAsync(id, cancellationToken);
        if (file is null)
        {
            return false;
        }

        await fileStorageService.DeleteAsync(
            file.StorageProvider,
            file.StoragePath,
            cancellationToken);
        return await fileRepository.DeleteAsync(id, cancellationToken);
    }
}
