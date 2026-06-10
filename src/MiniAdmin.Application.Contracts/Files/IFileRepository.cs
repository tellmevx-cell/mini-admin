using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Files;

public interface IFileRepository
{
    Task<PageResult<FileDto>> GetListAsync(
        FileListQuery query,
        CancellationToken cancellationToken = default);

    Task<FileDto?> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<FileDto> CreateAsync(
        CreateFileRecordRequest request,
        CancellationToken cancellationToken = default);

    Task<FileDto?> MarkInvalidAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
