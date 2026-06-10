namespace MiniAdmin.Application.Contracts.Users;

public sealed record UserExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record UserImportRowDto(
    int RowNumber,
    string UserName,
    string RealName,
    string Password,
    string? DepartmentCode,
    string? PositionCode,
    IReadOnlyList<string> RoleCodes,
    bool IsEnabled);

public sealed record UserImportResultDto(
    int CreatedCount,
    IReadOnlyList<UserImportErrorDto> Errors);

public sealed record UserImportErrorDto(
    int RowNumber,
    string UserName,
    string Message);

