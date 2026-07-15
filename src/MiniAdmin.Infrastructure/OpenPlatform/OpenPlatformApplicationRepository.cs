using System.Security.Cryptography;
using System.Text.Json;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Application.Contracts.OpenPlatform;
using OpenIddict.Abstractions;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public sealed class OpenPlatformApplicationRepository(
    IOpenIddictApplicationManager applicationManager,
    ICurrentTenant currentTenant) : IOpenPlatformApplicationRepository
{
    public async Task<IReadOnlyList<OpenPlatformApplicationDto>> GetListAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new List<OpenPlatformApplicationDto>();
        await foreach (var application in applicationManager
            .ListAsync(null, null, cancellationToken)
            .WithCancellation(cancellationToken))
        {
            var tenantId = await GetTenantIdAsync(application, cancellationToken);
            if (currentTenant.TenantId.HasValue && tenantId != currentTenant.TenantId)
            {
                continue;
            }

            results.Add(await ToDtoAsync(application, tenantId, cancellationToken));
        }

        return results
            .OrderBy(application => application.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<OpenPlatformApplicationSecretDto> CreateAsync(
        CreateOpenPlatformApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        var clientId = $"ma_{Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant()}";
        var clientSecret = request.ClientType == "Confidential"
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()
            : string.Empty;
        var scopes = NormalizeScopes(request.Scopes);
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ApplicationType = OpenIddictConstants.ApplicationTypes.Web,
            ClientId = clientId,
            ClientSecret = string.IsNullOrEmpty(clientSecret) ? null : clientSecret,
            ClientType = request.ClientType == "Confidential"
                ? OpenIddictConstants.ClientTypes.Confidential
                : OpenIddictConstants.ClientTypes.Public,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
            DisplayName = request.DisplayName.Trim()
        };

        foreach (var uri in request.RedirectUris.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            descriptor.RedirectUris.Add(new Uri(uri));
        }

        foreach (var uri in (request.PostLogoutRedirectUris ?? []).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        }

        descriptor.Permissions.UnionWith(
        [
            OpenIddictConstants.Permissions.Endpoints.Authorization,
            OpenIddictConstants.Permissions.Endpoints.Token,
            OpenIddictConstants.Permissions.Endpoints.Revocation,
            OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
            OpenIddictConstants.Permissions.GrantTypes.RefreshToken,
            OpenIddictConstants.Permissions.ResponseTypes.Code
        ]);
        descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        foreach (var scope in scopes)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        if (request.AllowClientCredentials)
        {
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
        }

        var now = DateTimeOffset.UtcNow;
        descriptor.Properties[OpenPlatformPropertyNames.TenantId] = JsonSerializer.SerializeToElement(
            currentTenant.TenantId?.ToString("D"));
        descriptor.Properties[OpenPlatformPropertyNames.CreatedAt] = JsonSerializer.SerializeToElement(now.ToString("O"));
        descriptor.Properties[OpenPlatformPropertyNames.ApiPermissions] = JsonSerializer.SerializeToElement(
            (request.ApiPermissions ?? [])
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)
                .ToArray());

        var application = await applicationManager.CreateAsync(descriptor, cancellationToken);
        return new OpenPlatformApplicationSecretDto(
            await ToDtoAsync(application, currentTenant.TenantId, cancellationToken),
            clientSecret);
    }

    public async Task<string?> RotateSecretAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var application = await FindWritableAsync(id, cancellationToken);
        if (application is null ||
            !string.Equals(
                await applicationManager.GetClientTypeAsync(application, cancellationToken),
                OpenIddictConstants.ClientTypes.Confidential,
                StringComparison.Ordinal))
        {
            return null;
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var descriptor = new OpenIddictApplicationDescriptor();
        await applicationManager.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.ClientSecret = secret;
        await applicationManager.UpdateAsync(application, descriptor, cancellationToken);
        return secret;
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var application = await FindWritableAsync(id, cancellationToken);
        if (application is null)
        {
            return false;
        }

        await applicationManager.DeleteAsync(application, cancellationToken);
        return true;
    }

    private async Task<object?> FindWritableAsync(Guid id, CancellationToken cancellationToken)
    {
        var application = await applicationManager.FindByIdAsync(id.ToString(), cancellationToken);
        if (application is null || !currentTenant.TenantId.HasValue)
        {
            return application;
        }

        return await GetTenantIdAsync(application, cancellationToken) == currentTenant.TenantId
            ? application
            : null;
    }

    private async Task<OpenPlatformApplicationDto> ToDtoAsync(
        object application,
        Guid? tenantId,
        CancellationToken cancellationToken)
    {
        var id = Guid.Parse((await applicationManager.GetIdAsync(application, cancellationToken))!);
        var permissions = await applicationManager.GetPermissionsAsync(application, cancellationToken);
        var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
        var scopes = permissions
            .Where(permission => permission.StartsWith(
                OpenIddictConstants.Permissions.Prefixes.Scope,
                StringComparison.Ordinal))
            .Select(permission => permission[OpenIddictConstants.Permissions.Prefixes.Scope.Length..])
            .OrderBy(scope => scope, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var createdAt = properties.TryGetValue(OpenPlatformPropertyNames.CreatedAt, out var createdValue) &&
            DateTimeOffset.TryParse(createdValue.GetString(), out var parsedCreatedAt)
                ? parsedCreatedAt
                : (DateTimeOffset?)null;
        var apiPermissions = properties.TryGetValue(OpenPlatformPropertyNames.ApiPermissions, out var permissionsValue) &&
            permissionsValue.ValueKind == JsonValueKind.Array
                ? permissionsValue.EnumerateArray()
                    .Select(value => value.GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray()
                : [];

        return new OpenPlatformApplicationDto(
            id,
            tenantId,
            (await applicationManager.GetClientIdAsync(application, cancellationToken))!,
            await applicationManager.GetDisplayNameAsync(application, cancellationToken) ?? string.Empty,
            ToPublicClientType(await applicationManager.GetClientTypeAsync(application, cancellationToken)),
            (await applicationManager.GetRedirectUrisAsync(application, cancellationToken))
                .ToArray(),
            (await applicationManager.GetPostLogoutRedirectUrisAsync(application, cancellationToken))
                .ToArray(),
            scopes,
            apiPermissions,
            permissions.Contains(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials),
            createdAt);
    }

    private async Task<Guid?> GetTenantIdAsync(
        object application,
        CancellationToken cancellationToken)
    {
        var properties = await applicationManager.GetPropertiesAsync(application, cancellationToken);
        return properties.TryGetValue(OpenPlatformPropertyNames.TenantId, out var value) &&
            Guid.TryParse(value.GetString(), out var tenantId)
                ? tenantId
                : null;
    }

    private static string[] NormalizeScopes(IReadOnlyList<string>? scopes)
    {
        var values = (scopes ?? [])
            .Append(OpenIddictConstants.Scopes.OpenId)
            .Append(OpenIddictConstants.Scopes.Profile)
            .Append(OpenPlatformScopeNames.Api)
            .Where(scope => !string.IsNullOrWhiteSpace(scope))
            .Select(scope => scope.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Roles,
            OpenIddictConstants.Scopes.OfflineAccess,
            OpenPlatformScopeNames.Api
        };
        if (values.Any(scope => !allowed.Contains(scope)))
        {
            throw new InvalidOperationException("存在不受支持的 OAuth/OIDC 授权范围。");
        }

        return values;
    }

    private static string ToPublicClientType(string? clientType)
    {
        return string.Equals(
            clientType,
            OpenIddictConstants.ClientTypes.Confidential,
            StringComparison.Ordinal)
                ? "Confidential"
                : "Public";
    }
}

public static class OpenPlatformScopeNames
{
    public const string Api = "miniadmin_api";
}
