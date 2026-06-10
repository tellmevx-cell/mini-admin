using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.CodeGenerators;

public interface ICodeGeneratorAppService
{
    Task<IReadOnlyList<CodeGeneratorTableDto>> GetTablesAsync(CancellationToken cancellationToken = default);

    Task<CodeGeneratorTableDto?> GetTableAsync(string tableName, CancellationToken cancellationToken = default);

    Task<CodeGeneratorPreviewResultDto> PreviewAsync(
        CodeGeneratorPreviewRequest request,
        CancellationToken cancellationToken = default);

    Task<CodeGenerationHistoryDto> GenerateAsync(
        CodeGeneratorGenerateRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<PageResult<CodeGenerationHistoryDto>> GetHistoriesAsync(
        CodeGeneratorHistoryListQuery query,
        CancellationToken cancellationToken = default);

    Task<CodeGenerationHistoryDetailDto?> GetHistoryDetailAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorRollbackResultDto> RollbackAsync(
        Guid id,
        CodeGeneratorRollbackRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorArtifactGovernanceResultDto> GetArtifactGovernanceAsync(
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorArtifactCleanupResultDto> CleanupArtifactAsync(
        string moduleName,
        CodeGeneratorArtifactCleanupRequest request,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);

    Task<CodeGeneratorArtifactRegisterHistoryResultDto> RegisterArtifactHistoryAsync(
        string moduleName,
        Guid? operatorUserId,
        string? operatorUserName,
        CancellationToken cancellationToken = default);
}
