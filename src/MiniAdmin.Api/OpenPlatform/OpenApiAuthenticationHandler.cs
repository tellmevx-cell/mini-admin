using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.OpenPlatform;

namespace MiniAdmin.Api.OpenPlatform;

public static class MiniAdminAuthenticationSchemes
{
    public const string Smart = "MiniAdmin.Smart";

    public const string AppKey = "MiniAdmin.AppKey";
}

public sealed partial class OpenApiAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration,
    IOpenApiCredentialRepository credentialRepository)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var appKey = Request.Headers["X-MA-AppKey"].ToString();
        var timestampValue = Request.Headers["X-MA-Timestamp"].ToString();
        var nonce = Request.Headers["X-MA-Nonce"].ToString();
        var signature = Request.Headers["X-MA-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(appKey) ||
            string.IsNullOrWhiteSpace(timestampValue) ||
            string.IsNullOrWhiteSpace(nonce) ||
            string.IsNullOrWhiteSpace(signature))
        {
            return AuthenticateResult.Fail("OpenAPI 签名请求头不完整。");
        }

        if (!AppKeyPattern().IsMatch(appKey) || !NoncePattern().IsMatch(nonce))
        {
            return AuthenticateResult.Fail("AppKey 或 Nonce 格式无效。");
        }

        if (!long.TryParse(timestampValue, out var timestampSeconds))
        {
            return AuthenticateResult.Fail("时间戳格式无效。");
        }

        DateTimeOffset timestamp;
        try
        {
            timestamp = DateTimeOffset.FromUnixTimeSeconds(timestampSeconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            return AuthenticateResult.Fail("时间戳超出有效范围。");
        }

        var now = TimeProvider.GetUtcNow();
        var windowSeconds = Math.Clamp(
            configuration.GetValue("OpenPlatform:SignatureWindowSeconds", 300),
            30,
            900);
        var window = TimeSpan.FromSeconds(windowSeconds);
        if ((now - timestamp).Duration() > window)
        {
            return AuthenticateResult.Fail("签名时间戳已过期。");
        }

        var credential = await credentialRepository.FindForValidationAsync(
            appKey,
            now,
            Context.RequestAborted);
        if (credential is null)
        {
            return AuthenticateResult.Fail("OpenAPI 凭证不存在、已撤销或已过期。");
        }

        Request.EnableBuffering();
        Request.Body.Position = 0;
        var bodyHash = Convert.ToHexString(await SHA256.HashDataAsync(
            Request.Body,
            Context.RequestAborted)).ToLowerInvariant();
        Request.Body.Position = 0;
        var canonical = OpenApiSignature.BuildCanonicalRequest(
            Request.Method,
            $"{Request.PathBase}{Request.Path}",
            OpenApiSignature.FlattenQuery(Request.Query),
            bodyHash,
            timestampValue,
            nonce);
        var expected = OpenApiSignature.Compute(credential.AppSecret, canonical);
        var normalizedSignature = signature.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase)
            ? signature[7..]
            : signature;
        if (!FixedTimeEquals(expected, normalizedSignature))
        {
            return AuthenticateResult.Fail("OpenAPI 请求签名无效。");
        }

        if (!await credentialRepository.TryUseNonceAsync(
                credential.CredentialId,
                nonce,
                timestamp.Add(window),
                now,
                Context.RequestAborted))
        {
            return AuthenticateResult.Fail("Nonce 已使用，请勿重放请求。");
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, credential.UserId.ToString()),
            new(ClaimTypes.Name, credential.UserName),
            new(OpenPlatformClaimTypes.PrincipalType, OpenPlatformClaimTypes.AppKey),
            new(OpenPlatformClaimTypes.ClientId, credential.AppKey),
            new("openapi_credential_id", credential.CredentialId.ToString())
        };
        if (credential.TenantId.HasValue)
        {
            claims.Add(new Claim("tenant_id", credential.TenantId.Value.ToString()));
        }

        claims.AddRange(credential.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(credential.Permissions.Select(permission => new Claim("permission", permission)));
        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            Scheme.Name));
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        if (expected.Length != actual.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.ASCII.GetBytes(expected),
            System.Text.Encoding.ASCII.GetBytes(actual.ToLowerInvariant()));
    }

    [GeneratedRegex("^ak_[a-f0-9]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex AppKeyPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{8,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex NoncePattern();
}
