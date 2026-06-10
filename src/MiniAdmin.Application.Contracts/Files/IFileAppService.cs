using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Files;

public interface IFileAppService
{
    Task<PageResult<FileDto>> GetListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default);

    Task<FileDto> UploadAsync(
        Stream content,
        string originalName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task<FileDownloadResult?> DownloadAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<FileDto?> MarkInvalidAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
