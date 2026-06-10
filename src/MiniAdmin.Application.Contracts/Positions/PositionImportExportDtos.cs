namespace MiniAdmin.Application.Contracts.Positions;

public sealed record PositionExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record PositionImportRowDto(
    int RowNumber,
    string Code,
    string Name,
    int Order,
    string? Remark,
    bool IsEnabled);

public sealed record PositionImportResultDto(
    int CreatedCount,
    IReadOnlyList<PositionImportErrorDto> Errors);

public sealed record PositionImportErrorDto(
    int RowNumber,
    string Code,
    string Message);
