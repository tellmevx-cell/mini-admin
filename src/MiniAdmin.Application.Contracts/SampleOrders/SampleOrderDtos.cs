namespace MiniAdmin.Application.Contracts.SampleOrders;

public sealed record SampleOrderDto(
    string Id,
    string? WorkflowInstanceId,
    string OriginalName,
    string StoredName,
    string ContentType,
    long Size,
    string StorageProvider,
    string StoragePath,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record SampleOrderListQuery(
    int Page = 1,
    int PageSize = 20,
    string? Keyword = null,
    string? OriginalName = null,
    string? StoredName = null,
    string? ContentType = null,
    long? Size = null,
    string? StorageProvider = null,
    string? StoragePath = null,
    string? Status = null);

public sealed record SaveSampleOrderRequest(
    string OriginalName,
    string StoredName,
    string ContentType,
    long Size,
    string StorageProvider,
    string StoragePath,
    string Status);

public sealed record SubmitSampleOrderWorkflowRequest(
    Guid DefinitionId,
    string? Comment);

public sealed record WithdrawSampleOrderWorkflowRequest(string? Comment);
