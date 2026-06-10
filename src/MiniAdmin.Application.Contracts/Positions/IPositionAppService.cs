using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Positions;

public interface IPositionAppService
{
    Task<PageResult<PositionDto>> GetListAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default);

    Task<PositionExportFileDto> ExportAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default);

    Task<PositionExportFileDto> GetImportTemplateAsync(
        CancellationToken cancellationToken = default);

    Task<PositionImportResultDto> PreviewImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<PositionImportResultDto> ImportAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<PositionExportFileDto> ExportImportErrorsAsync(
        Stream stream,
        CancellationToken cancellationToken = default);

    Task<PositionDto> CreateAsync(
        SavePositionRequest request,
        CancellationToken cancellationToken = default);

    Task<PositionDto?> UpdateAsync(
        Guid id,
        SavePositionRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
