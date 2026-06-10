namespace MiniAdmin.Domain.Entities;

public sealed class CodeGenerationHistory
{
    public Guid Id { get; set; }

    public string TableName { get; set; } = string.Empty;

    public string ModuleName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string PermissionPrefix { get; set; } = string.Empty;

    public string TenantMode { get; set; } = string.Empty;

    public string RequestJson { get; set; } = string.Empty;

    public string FilesJson { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public Guid? OperatorUserId { get; set; }

    public string? OperatorUserName { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
