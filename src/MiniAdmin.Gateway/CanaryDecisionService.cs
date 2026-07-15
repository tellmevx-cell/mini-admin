using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace MiniAdmin.Gateway;

public sealed class CanaryDecisionService(IOptions<GatewayCanaryOptions> options)
{
    private readonly GatewayCanaryOptions options = options.Value;

    public CanaryDecision Decide(HttpContext context)
    {
        var tenantId = GetIdentityValue(
            context,
            options.TenantHeaderName,
            "tenant_id");
        var userId = GetIdentityValue(
            context,
            options.UserHeaderName,
            "sub");
        var routingKey = GetRoutingKey(context, tenantId, userId);

        if (!options.Enabled)
        {
            return new CanaryDecision(false, "Canary routing is disabled.", routingKey);
        }

        var releaseHeader = context.Request.Headers[options.ReleaseHeaderName].ToString();
        if (releaseHeader.Equals(options.StableHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            return new CanaryDecision(false, "The request explicitly selected stable.", routingKey);
        }

        if (releaseHeader.Equals(options.CanaryHeaderValue, StringComparison.OrdinalIgnoreCase))
        {
            return new CanaryDecision(true, "The request explicitly selected canary.", routingKey);
        }

        if (options.HeaderMatches.Any(rule =>
                context.Request.Headers[rule.Key].ToString()
                    .Equals(rule.Value, StringComparison.OrdinalIgnoreCase)))
        {
            return new CanaryDecision(true, "A configured request-header rule matched.", routingKey);
        }

        if (MatchesWhitelist(tenantId, options.TenantWhitelist))
        {
            return new CanaryDecision(true, "The tenant is in the canary whitelist.", routingKey);
        }

        if (MatchesWhitelist(userId, options.UserWhitelist))
        {
            return new CanaryDecision(true, "The user is in the canary whitelist.", routingKey);
        }

        var ipAddress = context.Connection.RemoteIpAddress?.ToString();
        if (MatchesWhitelist(ipAddress, options.IpWhitelist))
        {
            return new CanaryDecision(true, "The client IP is in the canary whitelist.", routingKey);
        }

        var percentage = Math.Clamp(options.Percentage, 0, 100);
        var bucket = CalculateBucket(routingKey);
        return bucket < percentage
            ? new CanaryDecision(true, $"Stable hash bucket {bucket} matched {percentage}%.", routingKey)
            : new CanaryDecision(false, $"Stable hash bucket {bucket} did not match {percentage}%.", routingKey);
    }

    public static int CalculateBucket(string routingKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(routingKey));
        return (int)(BinaryPrimitives.ReadUInt32BigEndian(hash) % 100);
    }

    private string GetRoutingKey(HttpContext context, string? tenantId, string? userId)
    {
        var explicitKey = context.Request.Headers[options.RoutingKeyHeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(explicitKey))
        {
            return $"routing:{explicitKey.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            return $"tenant:{tenantId}";
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"user:{userId}";
        }

        var authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
        {
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(authorization))).ToLowerInvariant();
            return $"token:{tokenHash[..24]}";
        }

        return $"ip:{context.Connection.RemoteIpAddress}";
    }

    private static string? GetIdentityValue(
        HttpContext context,
        string headerName,
        string jwtClaimName)
    {
        var headerValue = context.Request.Headers[headerName].ToString();
        if (!string.IsNullOrWhiteSpace(headerValue))
        {
            return headerValue.Trim();
        }

        return TryReadJwtClaim(context.Request.Headers.Authorization.ToString(), jwtClaimName);
    }

    private static string? TryReadJwtClaim(string authorization, string claimName)
    {
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authorization["Bearer ".Length..].Trim();
        var parts = token.Split('.');
        if (parts.Length != 3 || parts[1].Length > 16_384)
        {
            return null;
        }

        try
        {
            var payload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            return document.RootElement.TryGetProperty(claimName, out var claim)
                ? claim.ToString()
                : null;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException)
        {
            return null;
        }
    }

    private static bool MatchesWhitelist(string? value, IEnumerable<string> whitelist)
    {
        return !string.IsNullOrWhiteSpace(value) && whitelist.Any(item =>
            value.Equals(item?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
