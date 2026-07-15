namespace MiniAdmin.Domain.Entities;

public sealed class OpenApiCredential
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public User User { get; set; } = null!;

    public Guid? TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public string Name { get; set; } = string.Empty;

    public string AppKey { get; set; } = string.Empty;

    public string SecretCiphertext { get; set; } = string.Empty;

    public string PermissionsJson { get; set; } = "[]";

    public bool IsEnabled { get; set; } = true;

    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastUsedAt { get; set; }
}
