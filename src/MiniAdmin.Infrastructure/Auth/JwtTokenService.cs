using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MiniAdmin.Application.Contracts.Auth;

namespace MiniAdmin.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public string CreateAccessToken(
        string userId,
        string userName,
        string sessionId,
        string? tenantId,
        string? tenantCode,
        string securityStamp,
        IReadOnlyList<string> roles,
        IReadOnlyList<string> permissionCodes)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.UniqueName, userName),
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userName),
            new("session_id", sessionId),
            new("security_stamp", securityStamp)
        };

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            claims.Add(new Claim("tenant_id", tenantId));
        }

        if (!string.IsNullOrWhiteSpace(tenantCode))
        {
            claims.Add(new Claim("tenant_code", tenantCode));
        }

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.ExpireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
