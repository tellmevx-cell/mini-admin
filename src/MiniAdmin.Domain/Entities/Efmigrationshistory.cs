namespace MiniAdmin.Domain.Entities;

public sealed class Efmigrationshistory
{
    public Guid Id { get; set; }

    private const string WorkflowBusinessType = "efmigrationshistory";

    public Guid? TenantId { get; set; }

    public string? WorkflowInstanceId { get; set; }

    public string ApprovalStatus { get; set; } = "Draft";

    public string ProductVersion { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public static string CreateBusinessKey(Guid id)
    {
        return $"{WorkflowBusinessType}:{id}";
    }

    public static bool TryParseBusinessKey(string? businessKey, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(businessKey))
        {
            return false;
        }

        var prefix = $"{WorkflowBusinessType}:";
        return businessKey.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
               Guid.TryParse(businessKey[prefix.Length..], out id);
    }
}
