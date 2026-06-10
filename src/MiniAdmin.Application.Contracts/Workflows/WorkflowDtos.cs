using MiniAdmin.Application.Contracts.Common;

namespace MiniAdmin.Application.Contracts.Workflows;

public sealed record WorkflowDefinitionListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    bool? IsEnabled = null);

public sealed record WorkflowInstanceListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? Status = null,
    string Scope = "all");

public sealed record WorkflowCcListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? InstanceStatus = null,
    string? ReadStatus = null);

public sealed record WorkflowBusinessBindingListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    bool? IsEnabled = null);

public sealed record WorkflowDefinitionDto(
    string Id,
    string Code,
    string Name,
    string? FormName,
    string? Description,
    string DesignerJson,
    bool IsEnabled,
    int Version,
    string PublishStatus,
    DateTimeOffset? PublishedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkflowNodeDto> Nodes,
    string FormSchemaJson = "[]");

public sealed record WorkflowDefinitionOptionDto(
    string Id,
    string Code,
    string Name,
    string? FormName,
    int Version,
    string FormSchemaJson = "[]");

public sealed record WorkflowBusinessBindingDto(
    string Id,
    string BusinessType,
    string BusinessName,
    string DefinitionId,
    string DefinitionCode,
    string DefinitionName,
    int DefinitionVersion,
    string DefinitionPublishStatus,
    bool IsEnabled,
    string? Remark,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkflowBusinessDefinitionDto(
    string BindingId,
    string BusinessType,
    string BusinessName,
    string DefinitionId,
    string DefinitionCode,
    string DefinitionName,
    string? FormName,
    int DefinitionVersion);

public sealed record WorkflowApproverUserOptionDto(
    string Id,
    string UserName,
    string RealName);

public sealed record WorkflowApproverRoleOptionDto(
    string Id,
    string Code,
    string Name,
    int EnabledUserCount);

public sealed record WorkflowNodeDto(
    string Id,
    string Name,
    string DesignerNodeId,
    string NodeType,
    string ApprovalMode,
    string ApproverType,
    string? ApproverUserId,
    string? ApproverUserName,
    string? ApproverRoleId,
    string? ApproverRoleName,
    int Order,
    bool IsEnabled,
    int? SlaMinutes = null);

public sealed record SaveWorkflowDefinitionRequest(
    string Code,
    string Name,
    string? FormName,
    string? Description,
    string? DesignerJson,
    bool IsEnabled,
    IReadOnlyList<SaveWorkflowNodeRequest> Nodes,
    string? FormSchemaJson = null);

public sealed record SaveWorkflowNodeRequest(
    string? DesignerNodeId,
    string Name,
    string ApproverType,
    Guid? ApproverUserId,
    Guid? ApproverRoleId,
    int Order,
    bool IsEnabled,
    string NodeType = "approve",
    string ApprovalMode = "Any",
    int? SlaMinutes = null);

public sealed record SaveWorkflowBusinessBindingRequest(
    string BusinessType,
    string BusinessName,
    Guid DefinitionId,
    bool IsEnabled,
    string? Remark);

public sealed record StartWorkflowInstanceRequest(
    Guid DefinitionId,
    string Title,
    string? BusinessKey,
    string? FormDataJson,
    IReadOnlyList<Guid>? AttachmentFileIds = null);

public sealed record WorkflowActionRequest(string? Comment);

public sealed record WorkflowRemindTaskRequest(string? Comment);

public sealed record WorkflowTransferTaskRequest(Guid TargetUserId, string? Comment);

public sealed record WorkflowAttachmentRequest(Guid FileId, string? Remark);

public sealed record WorkflowCommentRequest(string Content);

public sealed record WorkflowUserContext(
    Guid UserId,
    string UserName,
    bool CanManageAllWorkflowInstances = false);

public interface IWorkflowBusinessStateHandler
{
    Task HandleAsync(WorkflowInstanceDto instance, CancellationToken cancellationToken = default);
}

public sealed record WorkflowInstanceDto(
    string Id,
    string DefinitionId,
    string DefinitionCode,
    string DefinitionName,
    int DefinitionVersion,
    string DefinitionSnapshotJson,
    string Title,
    string? BusinessKey,
    string FormDataJson,
    string Status,
    string? CurrentNodeId,
    string? CurrentNodeName,
    string InitiatorUserId,
    string InitiatorUserName,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    IReadOnlyList<WorkflowTaskDto> Tasks,
    IReadOnlyList<WorkflowActionLogDto> ActionLogs,
    IReadOnlyList<WorkflowAttachmentDto> Attachments,
    IReadOnlyList<WorkflowCommentDto> Comments,
    IReadOnlyList<WorkflowCcRecordDto> CcRecords);

public sealed record WorkflowAttachmentDto(
    string Id,
    string InstanceId,
    string FileId,
    string OriginalName,
    string ContentType,
    long Size,
    string StorageProvider,
    string StoragePath,
    string? Remark,
    string UploaderUserId,
    string UploaderUserName,
    DateTimeOffset CreatedAt);

public sealed record WorkflowCommentDto(
    string Id,
    string InstanceId,
    string Content,
    string AuthorUserId,
    string AuthorUserName,
    DateTimeOffset CreatedAt);

public sealed record WorkflowAttachmentDownloadDto(
    string AttachmentId,
    string InstanceId,
    string FileId,
    string OriginalName,
    string ContentType);

public sealed record WorkflowTaskDto(
    string Id,
    string InstanceId,
    string InstanceTitle,
    string DefinitionName,
    string NodeId,
    string NodeName,
    string ApproverUserId,
    string ApproverUserName,
    string Status,
    string? Comment,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? DueAt = null,
    DateTimeOffset? LastAutoRemindedAt = null,
    bool IsOverdue = false);

public sealed record WorkflowCcRecordDto(
    string Id,
    string InstanceId,
    string InstanceTitle,
    string DefinitionName,
    string? BusinessKey,
    string InstanceStatus,
    string? CurrentNodeName,
    string NodeId,
    string NodeName,
    string RecipientUserId,
    string RecipientUserName,
    string? SenderUserId,
    string? SenderUserName,
    string InitiatorUserId,
    string InitiatorUserName,
    DateTimeOffset StartedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReadAt,
    bool IsRead,
    string ReadStatus);

public sealed record WorkflowSlaScanDetailDto(
    string TaskId,
    string InstanceId,
    string InstanceTitle,
    string NodeName,
    string ApproverUserId,
    string ApproverUserName,
    DateTimeOffset DueAt);

public sealed record WorkflowSlaScanResultDto(
    int OverdueTaskCount,
    int RemindedTaskCount,
    IReadOnlyList<WorkflowSlaScanDetailDto> Details);

public sealed record WorkflowActionLogDto(
    string Id,
    string Action,
    string? NodeId,
    string? NodeName,
    string OperatorUserId,
    string OperatorUserName,
    string? Comment,
    DateTimeOffset CreatedAt);

public interface IWorkflowAppService
{
    Task<PageResult<WorkflowDefinitionDto>> GetDefinitionsAsync(
        WorkflowDefinitionListQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowDefinitionOptionDto>> GetDefinitionOptionsAsync(
        CancellationToken cancellationToken = default);

    Task<PageResult<WorkflowBusinessBindingDto>> GetBusinessBindingsAsync(
        WorkflowBusinessBindingListQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkflowBusinessDefinitionDto?> ResolveBusinessDefinitionAsync(
        string businessType,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDto> CreateDefinitionAsync(
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDto?> UpdateDefinitionAsync(
        Guid id,
        SaveWorkflowDefinitionRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDto?> PublishDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowDefinitionDto?> CreateNewVersionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingDto> CreateBusinessBindingAsync(
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<WorkflowBusinessBindingDto?> UpdateBusinessBindingAsync(
        Guid id,
        SaveWorkflowBusinessBindingRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteBusinessBindingAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PageResult<WorkflowInstanceDto>> GetInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<PageResult<WorkflowInstanceDto>> GetCcInstancesAsync(
        WorkflowInstanceListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<PageResult<WorkflowCcRecordDto>> GetCcRecordsAsync(
        WorkflowCcListQuery query,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowCcRecordDto?> MarkCcRecordAsReadAsync(
        Guid ccRecordId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto?> GetInstanceAsync(
        Guid id,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskDto>> GetTodoTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskDto>> GetDoneTasksAsync(
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto> StartInstanceAsync(
        StartWorkflowInstanceRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto?> AddAttachmentAsync(
        Guid instanceId,
        WorkflowAttachmentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowCommentDto?> AddCommentAsync(
        Guid instanceId,
        WorkflowCommentRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowAttachmentDownloadDto?> GetAttachmentDownloadAsync(
        Guid instanceId,
        Guid attachmentId,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto?> ApproveAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto?> RejectAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowTaskDto?> TransferTaskAsync(
        Guid taskId,
        WorkflowTransferTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowTaskDto?> RemindTaskAsync(
        Guid taskId,
        WorkflowRemindTaskRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);

    Task<WorkflowSlaScanResultDto> ScanOverdueTasksAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<WorkflowInstanceDto?> WithdrawAsync(
        Guid instanceId,
        WorkflowActionRequest request,
        WorkflowUserContext user,
        CancellationToken cancellationToken = default);
}

public interface IWorkflowRepository : IWorkflowAppService;

public sealed class WorkflowOperationException(string message) : InvalidOperationException(message);
