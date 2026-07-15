using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Domain.Shared.MultiTenancy;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Infrastructure.OpenPlatform;

public sealed class OpenApiCredentialRepository(
    MiniAdminDbContext dbContext,
    IOpenApiSecretProtector secretProtector) : IOpenApiCredentialRepository
{
    public async Task<IReadOnlyList<OpenApiCredentialDto>> GetMyAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var credentials = await dbContext.OpenApiCredentials
            .AsNoTracking()
            .Where(credential => credential.UserId == userId)
            .OrderByDescending(credential => credential.CreatedAt)
            .ToArrayAsync(cancellationToken);
        return credentials.Select(ToDto).ToArray();
    }

    public async Task<OpenApiCredentialSecretDto> CreateAsync(
        Guid userId,
        Guid? tenantId,
        CreateOpenApiCredentialRequest request,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var userExists = await dbContext.Users.AnyAsync(
            user => user.Id == userId && user.IsEnabled && user.TenantId == tenantId,
            cancellationToken);
        if (!userExists)
        {
            throw new InvalidOperationException("当前用户不存在、已停用或租户不匹配。");
        }

        var appSecret = $"sk_{Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant()}";
        var credential = new OpenApiCredential
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TenantId = tenantId,
            Name = request.Name.Trim(),
            AppKey = $"ak_{Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant()}",
            SecretCiphertext = secretProtector.Protect(appSecret),
            PermissionsJson = JsonSerializer.Serialize(request.Permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(permission => permission, StringComparer.OrdinalIgnoreCase)),
            IsEnabled = true,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.OpenApiCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new OpenApiCredentialSecretDto(ToDto(credential), appSecret);
    }

    public async Task<bool> RevokeAsync(
        Guid userId,
        Guid id,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.OpenApiCredentials.SingleOrDefaultAsync(
            item => item.Id == id && item.UserId == userId,
            cancellationToken);
        if (credential is null)
        {
            return false;
        }

        credential.IsEnabled = false;
        credential.UpdatedAt = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<OpenApiCredentialValidationDto?> FindForValidationAsync(
        string appKey,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.OpenApiCredentials
            .AsNoTracking()
            .Include(item => item.Tenant)
            .Include(item => item.User)
                .ThenInclude(user => user.UserRoles)
                    .ThenInclude(userRole => userRole.Role)
                        .ThenInclude(role => role.RoleMenus)
                            .ThenInclude(roleMenu => roleMenu.Menu)
            .SingleOrDefaultAsync(item =>
                item.AppKey == appKey &&
                item.IsEnabled &&
                (!item.ExpiresAt.HasValue || item.ExpiresAt > now) &&
                item.User.IsEnabled,
                cancellationToken);
        if (credential is null ||
            credential.User.TenantId != credential.TenantId ||
            credential.TenantId.HasValue &&
            (credential.Tenant is null ||
             credential.Tenant.Status != TenantStatus.Active ||
             credential.Tenant.ExpireAt.HasValue && credential.Tenant.ExpireAt <= now))
        {
            return null;
        }

        var grantedPermissions = credential.User.UserRoles
            .Where(item => item.Role.IsEnabled)
            .SelectMany(item => item.Role.RoleMenus)
            .Where(item => item.Menu.IsEnabled && !string.IsNullOrWhiteSpace(item.Menu.PermissionCode))
            .Select(item => item.Menu.PermissionCode!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var credentialPermissions = DeserializePermissions(credential.PermissionsJson)
            .Where(grantedPermissions.Contains)
            .ToArray();
        var roles = credential.User.UserRoles
            .Where(item => item.Role.IsEnabled)
            .Select(item => item.Role.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new OpenApiCredentialValidationDto(
            credential.Id,
            credential.UserId,
            credential.TenantId,
            credential.User.UserName,
            credential.AppKey,
            secretProtector.Unprotect(credential.SecretCiphertext),
            roles,
            credentialPermissions);
    }

    public async Task<bool> TryUseNonceAsync(
        Guid credentialId,
        string nonce,
        DateTimeOffset expiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.OpenApiNonces.AsNoTracking().AnyAsync(
                item => item.CredentialId == credentialId && item.Nonce == nonce,
                cancellationToken))
        {
            return false;
        }

        var expired = await dbContext.OpenApiNonces
            .Where(item => item.ExpiresAt <= now)
            .Take(100)
            .ToArrayAsync(cancellationToken);
        dbContext.OpenApiNonces.RemoveRange(expired);
        dbContext.OpenApiNonces.Add(new OpenApiNonce
        {
            Id = Guid.NewGuid(),
            CredentialId = credentialId,
            Nonce = nonce,
            ExpiresAt = expiresAt,
            CreatedAt = now
        });
        var credential = await dbContext.OpenApiCredentials.SingleAsync(
            item => item.Id == credentialId,
            cancellationToken);
        credential.LastUsedAt = now;
        credential.UpdatedAt = now;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            return false;
        }
    }

    private static OpenApiCredentialDto ToDto(OpenApiCredential credential)
    {
        return new OpenApiCredentialDto(
            credential.Id,
            credential.Name,
            credential.AppKey,
            DeserializePermissions(credential.PermissionsJson),
            credential.IsEnabled,
            credential.ExpiresAt,
            credential.CreatedAt,
            credential.LastUsedAt);
    }

    private static string[] DeserializePermissions(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
