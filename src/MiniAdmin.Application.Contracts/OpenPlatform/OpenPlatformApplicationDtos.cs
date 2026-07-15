namespace MiniAdmin.Application.Contracts.OpenPlatform;

public sealed record OpenPlatformApplicationDto(
    Guid Id,
    Guid? TenantId,
    string ClientId,
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> ApiPermissions,
    bool AllowsClientCredentials,
    DateTimeOffset? CreatedAt);

public sealed record CreateOpenPlatformApplicationRequest(
    string DisplayName,
    string ClientType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string>? PostLogoutRedirectUris = null,
    IReadOnlyList<string>? Scopes = null,
    IReadOnlyList<string>? ApiPermissions = null,
    bool AllowClientCredentials = false);

public sealed record OpenPlatformUserDto(
    Guid Id,
    Guid? TenantId,
    string UserName,
    string RealName,
    string? Email,
    string SecurityStamp,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public static class OpenPlatformClaimTypes
{
    public const string PrincipalType = "miniadmin_principal_type";

    public const string ClientId = "miniadmin_client_id";

    public const string Application = "application";

    public const string User = "user";

    public const string AppKey = "app_key";
}

public static class OpenPlatformPropertyNames
{
    public const string TenantId = "miniadmin:tenant_id";

    public const string CreatedAt = "miniadmin:created_at";

    public const string ApiPermissions = "miniadmin:api_permissions";
}

public sealed record OpenPlatformApplicationSecretDto(
    OpenPlatformApplicationDto Application,
    string ClientSecret);

public interface IOpenPlatformApplicationRepository
{
    Task<IReadOnlyList<OpenPlatformApplicationDto>> GetListAsync(
        CancellationToken cancellationToken = default);

    Task<OpenPlatformApplicationSecretDto> CreateAsync(
        CreateOpenPlatformApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<string?> RotateSecretAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public interface IOpenPlatformUserRepository
{
    Task<OpenPlatformUserDto?> FindByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public interface IOpenPlatformApplicationAppService
{
    Task<IReadOnlyList<OpenPlatformApplicationDto>> GetListAsync(
        CancellationToken cancellationToken = default);

    Task<OpenPlatformApplicationSecretDto> CreateAsync(
        CreateOpenPlatformApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<string?> RotateSecretAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}

public sealed record OpenApiCredentialDto(
    Guid Id,
    string Name,
    string AppKey,
    IReadOnlyList<string> Permissions,
    bool IsEnabled,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt);

public sealed record CreateOpenApiCredentialRequest(
    string Name,
    IReadOnlyList<string> Permissions,
    DateTimeOffset? ExpiresAt = null);

public sealed record OpenApiCredentialSecretDto(
    OpenApiCredentialDto Credential,
    string AppSecret);

public sealed record OpenApiCredentialValidationDto(
    Guid CredentialId,
    Guid UserId,
    Guid? TenantId,
    string UserName,
    string AppKey,
    string AppSecret,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public interface IOpenApiCredentialRepository
{
    Task<IReadOnlyList<OpenApiCredentialDto>> GetMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<OpenApiCredentialSecretDto> CreateAsync(
        Guid userId,
        Guid? tenantId,
        CreateOpenApiCredentialRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid userId,
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<OpenApiCredentialValidationDto?> FindForValidationAsync(
        string appKey,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<bool> TryUseNonceAsync(
        Guid credentialId,
        string nonce,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}

public interface IOpenApiCredentialAppService
{
    Task<IReadOnlyList<OpenApiCredentialDto>> GetMyAsync(
        CancellationToken cancellationToken = default);

    Task<OpenApiCredentialSecretDto> CreateAsync(
        CreateOpenApiCredentialRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
