namespace MiniAdmin.Application.Contracts.Efmigrationshistorys;

public sealed record EfmigrationshistoryDto(
    string Id,
    string? WorkflowInstanceId,
    string ApprovalStatus,
    string ProductVersion,
    DateTimeOffset CreatedAt);

public sealed record EfmigrationshistoryListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? ProductVersion = null);

public sealed record SaveEfmigrationshistoryRequest(
    string ProductVersion);

public sealed record SubmitEfmigrationshistoryWorkflowRequest(string? Comment);

public sealed record WithdrawEfmigrationshistoryWorkflowRequest(string? Comment);

public sealed record EfmigrationshistoryExportFileDto(
    string FileName,
    string ContentType,
    byte[] Content);

public sealed record EfmigrationshistoryImportResultDto(
    int CreatedCount,
    IReadOnlyList<EfmigrationshistoryImportErrorDto> Errors);

public sealed record EfmigrationshistoryImportErrorDto(
    int RowNumber,
    string Field,
    string Message);