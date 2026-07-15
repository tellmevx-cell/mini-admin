namespace MiniAdmin.Domain.Entities;

public sealed class OpenApiNonce
{
    public Guid Id { get; set; }

    public Guid CredentialId { get; set; }

    public OpenApiCredential Credential { get; set; } = null!;

    public string Nonce { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
