using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Positions;

public interface IPositionRepository
{
    Task<PageResult<PositionDto>> GetListAsync(
        PositionListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PositionDto>> GetExportListAsync(
        PositionListQuery query,
        int limit = 10000,
        CancellationToken cancellationToken = default);

    Task<PositionImportResultDto> ValidateImportAsync(
        IReadOnlyList<PositionImportRowDto> rows,
        CancellationToken cancellationToken = default);

    Task<PositionImportResultDto> ImportAsync(
        IReadOnlyList<PositionImportRowDto> rows,
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
