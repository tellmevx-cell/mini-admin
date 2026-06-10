using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.CodeGenerators;

public sealed record CodeGeneratorTableDto(
    string TableName,
    string TableComment,
    IReadOnlyList<CodeGeneratorColumnDto> Columns,
    CodeGeneratorExistingModuleDto? ExistingModule = null,
    string? GenerationBlockReason = null);

public sealed record CodeGeneratorExistingModuleDto(
    string TableName,
    string ModuleName,
    string ModuleKind,
    string? RoutePath,
    string? Component,
    IReadOnlyList<string> Files);

public sealed record CodeGeneratorColumnDto(
    string ColumnName,
    string ColumnType,
    string DotNetType,
    string TsType,
    string ColumnComment,
    bool IsPrimaryKey,
    bool IsNullable,
    int Sort);

public sealed record CodeGeneratorFieldConfigDto(
    string ColumnName,
    string PropertyName,
    string DisplayName,
    string DotNetType,
    string TsType,
    bool IsPrimaryKey,
    bool IsRequired,
    bool ListVisible,
    bool QueryVisible,
    bool CreateVisible,
    bool UpdateVisible,
    string ControlType,
    string? DictionaryCode,
    int Sort,
    string QueryMode = "Contains",
    int? MaxLength = null,
    bool IsUnique = false,
    string? DefaultValue = null);

public sealed record CodeGeneratorPreviewRequest(
    string TableName,
    string ModuleName,
    string BusinessName,
    string RoutePath,
    string? ParentMenuId,
    string PermissionPrefix,
    string TenantMode,
    IReadOnlyList<CodeGeneratorFieldConfigDto> Fields,
    string DataScopeMode = "None",
    string? DataScopeField = null,
    bool EnableAudit = true,
    bool EnableImportExport = false,
    bool EnableWorkflow = false,
    string? WorkflowBusinessType = null);

public sealed record CodeGeneratorPreviewFileDto(
    string RelativePath,
    string Content,
    bool HasConflict);

public sealed record CodeGeneratorPreviewResultDto(
    IReadOnlyList<CodeGeneratorPreviewFileDto> Files,
    IReadOnlyList<string> PermissionCodes,
    bool HasConflicts,
    CodeGeneratorInstallPlanDto InstallPlan);

public sealed record CodeGeneratorInstallPlanDto(
    bool TableExists,
    string? CreateTableSql,
    IReadOnlyList<CodeGeneratorInstallStepDto> Steps);

public sealed record CodeGeneratorInstallStepDto(
    string Key,
    string Title,
    string Description,
    string Status);

public sealed record CodeGeneratorGenerateRequest(
    CodeGeneratorPreviewRequest Preview,
    bool Overwrite = false,
    bool AutoInstall = true);

public sealed record CodeGeneratorGeneratedMenuInstallDto(
    Guid Id,
    Guid? ParentId,
    string Name,
    string Path,
    string? Component,
    string Title,
    string? Icon,
    int Order,
    string? PermissionCode,
    bool IsEnabled,
    bool IsVisible);

public sealed record CodeGeneratorAutoInstallRequestDto(
    string? CreateTableSql,
    IReadOnlyList<CodeGeneratorGeneratedMenuInstallDto> Menus);

public sealed record CodeGeneratorAutoInstallResultDto(
    bool TableInstalled,
    bool TableSkipped,
    bool MenuPermissionsInstalled);

public sealed record CodeGeneratorRollbackRequest(
    bool DropTable = false);

public sealed record CodeGeneratorTableDropResultDto(
    bool TableDropped,
    bool TableDropSkipped,
    string? TableDropMessage);

public sealed record CodeGenerationHistoryDto(
    string Id,
    string TableName,
    string ModuleName,
    string BusinessName,
    string PermissionPrefix,
    string TenantMode,
    string Status,
    string? ErrorMessage,
    IReadOnlyList<CodeGeneratorPreviewFileDto> Files,
    DateTimeOffset CreatedAt);

public sealed record CodeGenerationHistoryDetailDto(
    string Id,
    string TableName,
    string ModuleName,
    string BusinessName,
    string PermissionPrefix,
    string TenantMode,
    string Status,
    string? ErrorMessage,
    string? OperatorUserName,
    CodeGeneratorPreviewRequest Preview,
    IReadOnlyList<CodeGeneratorPreviewFileDto> Files,
    CodeGeneratorInstallPlanDto InstallPlan,
    DateTimeOffset CreatedAt);

public sealed record CodeGenerationHistoryDetailSourceDto(
    string Id,
    string TableName,
    string ModuleName,
    string BusinessName,
    string PermissionPrefix,
    string TenantMode,
    string Status,
    string? ErrorMessage,
    string? OperatorUserName,
    CodeGeneratorPreviewRequest Preview,
    IReadOnlyList<CodeGeneratorPreviewFileDto> Files,
    DateTimeOffset CreatedAt);

public sealed record CodeGeneratorRollbackResultDto(
    string Id,
    string Status,
    int DeletedFileCount,
    int DeletedMenuCount,
    bool TableDropped,
    bool TableDropSkipped,
    string? TableDropMessage);

public sealed record CodeGeneratorHistoryListQuery(
    int Page = 1,
    int PageSize = 20,
    string? ModuleName = null,
    string? TableName = null,
    string? Status = null);

public sealed record CodeGeneratorHistoryPageResult(PageResult<CodeGenerationHistoryDto> Page);

public sealed record CodeGeneratorArtifactGovernanceDto(
    string ModuleName,
    string TableName,
    string ModuleKind,
    string? RoutePath,
    string? Component,
    bool HasHistory,
    bool HasMenuPermissions,
    bool IsMapped,
    bool IsReservedTable,
    string? RiskReason,
    IReadOnlyList<string> Files);

public sealed record CodeGeneratorArtifactGovernanceResultDto(
    IReadOnlyList<CodeGeneratorArtifactGovernanceDto> Items);

public sealed record CodeGeneratorArtifactCleanupRequest(
    bool DropTable = false);

public sealed record CodeGeneratorArtifactCleanupResultDto(
    string ModuleName,
    int DeletedFileCount,
    int DeletedMenuCount,
    bool TableDropped,
    bool TableDropSkipped,
    string? TableDropMessage);

public sealed record CodeGeneratorArtifactRegisterHistoryResultDto(
    CodeGenerationHistoryDto History);
