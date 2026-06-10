namespace MiniAdmin.Domain.Entities;

public sealed class SampleOrder
{
    public const string DraftStatus = "Draft";

    public const string PendingApprovalStatus = "PendingApproval";

    public const string ApprovedStatus = "Approved";

    public const string RejectedStatus = "Rejected";

    public const string WithdrawnStatus = "Withdrawn";

    public const string BusinessKeyPrefix = "sample-order:";

    public Guid Id { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? WorkflowInstanceId { get; set; }

    public string OriginalName { get; set; } = string.Empty;

    public string StoredName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long Size { get; set; }

    public string StorageProvider { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static string CreateBusinessKey(Guid id)
    {
        return $"{BusinessKeyPrefix}{id:D}";
    }

    public static bool TryParseBusinessKey(string? businessKey, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(businessKey) ||
            !businessKey.StartsWith(BusinessKeyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Guid.TryParse(businessKey[BusinessKeyPrefix.Length..], out id);
    }

    public bool CanSubmitWorkflow()
    {
        return Status is DraftStatus or RejectedStatus or WithdrawnStatus;
    }

    public bool CanModify()
    {
        return Status is not PendingApprovalStatus and not ApprovedStatus;
    }
}
