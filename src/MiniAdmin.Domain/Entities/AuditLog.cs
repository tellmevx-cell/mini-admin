namespace MiniAdmin.Domain.Entities;

public sealed class AuditLog
{
    public Guid Id { get; set; }

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public string? QueryString { get; set; }

    public string Module { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string? ResourceId { get; set; }

    public int StatusCode { get; set; }

    public bool IsSuccess { get; set; }

    public long ElapsedMilliseconds { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public string RequestBody { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<AuditEntityChange> EntityChanges { get; set; } = new List<AuditEntityChange>();
}
