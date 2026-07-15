using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using MiniAdmin.Api.OpenPlatform;
using MiniAdmin.Application.Contracts.OpenPlatform;
using MiniAdmin.Domain.Entities;
using MiniAdmin.Infrastructure.OpenPlatform;
using MiniAdmin.Infrastructure.Persistence;

namespace MiniAdmin.Tests;

public sealed class OpenPlatformSecurityTests
{
    [Fact]
    public void Canonical_request_is_stable_and_hmac_matches_standard_sha256()
    {
        var canonical = OpenApiSignature.BuildCanonicalRequest(
            "post",
            "/api/orders",
            [
                new KeyValuePair<string, string>("z", "last"),
                new KeyValuePair<string, string>("q", "a value"),
                new KeyValuePair<string, string>("q", "first")
            ],
            "AABB",
            "1720000000",
            "nonce_123456");
        const string secret = "sk_test_secret";
        var expected = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();

        Assert.Equal(
            "POST\n/api/orders\nq=a%20value&q=first&z=last\naabb\n1720000000\nnonce_123456",
            canonical);
        Assert.Equal(expected, OpenApiSignature.Compute(secret, canonical));
    }

    [Fact]
    public async Task Credential_secret_is_encrypted_and_nonce_cannot_be_reused()
    {
        await using var dbContext = CreateDbContext();
        var user = new User
        {
            Id = Guid.NewGuid(),
            UserName = "openapi-user",
            RealName = "OpenAPI User",
            PasswordHash = "test",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsEnabled = true
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var protector = new OpenApiSecretProtector(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = "test-signing-key-with-at-least-thirty-two-bytes"
            })
            .Build());
        var repository = new OpenApiCredentialRepository(dbContext, protector);
        var now = DateTimeOffset.UtcNow;

        var created = await repository.CreateAsync(
            user.Id,
            null,
            new CreateOpenApiCredentialRequest("automation", []),
            now);
        var stored = await dbContext.OpenApiCredentials.AsNoTracking().SingleAsync();
        var validation = await repository.FindForValidationAsync(created.Credential.AppKey, now);
        var firstUse = await repository.TryUseNonceAsync(
            created.Credential.Id,
            "nonce_123456",
            now.AddMinutes(5),
            now);
        var replay = await repository.TryUseNonceAsync(
            created.Credential.Id,
            "nonce_123456",
            now.AddMinutes(5),
            now);

        Assert.DoesNotContain(created.AppSecret, stored.SecretCiphertext, StringComparison.Ordinal);
        Assert.Equal(created.AppSecret, validation?.AppSecret);
        Assert.True(firstUse);
        Assert.False(replay);
    }

    private static MiniAdminDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MiniAdminDbContext>()
            .UseInMemoryDatabase($"open-platform-tests-{Guid.NewGuid():N}")
            .Options;
        return new MiniAdminDbContext(options);
    }
}
