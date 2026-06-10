using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.CodeGenerators;

public interface ICodeGeneratorRepository
{
    Task<IReadOnlyList<CodeGeneratorTableDto>> GetTablesAsync(CancellationToken cancellationToken = default);

    Task<CodeGeneratorTableDto?> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

    Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken = default);

    Task<bool> GeneratedMenusInstalledAsync(
        IReadOnlyList<Guid> menuIds,
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorAutoInstallResultDto> AutoInstallAsync(
        CodeGeneratorAutoInstallRequestDto request,
        CancellationToken cancellationToken = default);

    Task<CodeGenerationHistoryDto> AddHistoryAsync(
        CodeGeneratorPreviewRequest request,
        IReadOnlyList<CodeGeneratorPreviewFileDto> files,
        string status,
        string? errorMessage,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<PageResult<CodeGenerationHistoryDto>> GetHistoriesAsync(
        CodeGeneratorHistoryListQuery query,
        CancellationToken cancellationToken = default);

    Task<CodeGenerationHistoryDetailSourceDto?> GetHistoryDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<int> RollbackGeneratedMenusAsync(
        Guid historyId,
        IReadOnlyList<Guid> menuIds,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<int> CleanupGeneratedMenusAsync(
        IReadOnlyList<Guid> menuIds,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorTableDropResultDto> DropGeneratedTableAsync(
        string tableName,
        CancellationToken cancellationToken = default);
}
