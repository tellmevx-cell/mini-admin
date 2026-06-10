using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Menus;
using MiniAdmin.Application.Contracts.MultiTenancy;
using MiniAdmin.Domain.Shared.MultiTenancy;

namespace MiniAdmin.Application.Auth;

public sealed class AuthAppService(
    IAuthRepository authRepository,
    IMenuRepository menuRepository,
    IPasswordService passwordService,
    ITokenService tokenService,
    ILoginSecurityService loginSecurityService,
    ITenantRepository tenantRepository) : IAuthAppService
{
    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        await loginSecurityService.ValidateBeforePasswordAsync(request, cancellationToken);

        var user = await authRepository.FindByUserNameAsync(request.Username, cancellationToken);
        if (user is null || !passwordService.VerifyPassword(user.PasswordHash, request.Password))
        {
            var failure = await loginSecurityService.RecordFailureAsync(
                request.Username,
                request.ClientIp,
                cancellationToken);
            throw new LoginFailureException("用户名或密码错误", failure.CaptchaRequired, failure.LockRemainingSeconds);
        }

        await loginSecurityService.ClearFailuresAsync(request.Username, request.ClientIp, cancellationToken);

        var tenant = await ResolveLoginTenantAsync(user, request, cancellationToken);
        var sessionId = Guid.NewGuid().ToString();
        var token = tokenService.CreateAccessToken(
            user.UserId,
            user.UserName,
            sessionId,
            tenant?.Id.ToString(),
            tenant?.Code,
            user.SecurityStamp,
            user.RoleCodes,
            user.PermissionCodes);

        return new LoginResult(token, sessionId, tenant?.Id.ToString(), tenant?.Code);
    }

    public Task<IReadOnlyList<string>> GetAccessCodesAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        return menuRepository.GetPermissionCodesByUserNameAsync(userName, cancellationToken);
    }

    private async Task<TenantLookupDto?> ResolveLoginTenantAsync(
        AuthenticatedUserDto user,
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var tenantCode = NormalizeTenantCode(request.TenantCode);
        if (!user.TenantId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                throw new LoginFailureException("平台用户不能使用租户编码登录", false, null);
            }

            return null;
        }

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            throw new LoginFailureException("请输入租户编码", false, null);
        }

        var tenant = await tenantRepository.FindByCodeAsync(tenantCode, cancellationToken);
        if (tenant is null || tenant.Id != user.TenantId.Value)
        {
            throw new LoginFailureException("租户编码或账号不匹配", false, null);
        }

        if (!IsTenantAvailable(tenant))
        {
            throw new LoginFailureException("租户已禁用或已过期", false, null);
        }

        return tenant;
    }

    private static bool IsTenantAvailable(TenantLookupDto tenant)
    {
        return tenant.Status == TenantStatus.Active &&
               (!tenant.ExpireAt.HasValue || tenant.ExpireAt.Value > DateTimeOffset.UtcNow);
    }

    private static string? NormalizeTenantCode(string? tenantCode)
    {
        return string.IsNullOrWhiteSpace(tenantCode) ? null : tenantCode.Trim();
    }
}
