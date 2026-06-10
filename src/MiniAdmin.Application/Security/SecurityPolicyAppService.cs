using MiniAdmin.Application.Contracts.Security;

namespace MiniAdmin.Application.Security;

public sealed class SecurityPolicyAppService(
    ISecurityPolicyRepository securityPolicyRepository) : ISecurityPolicyAppService
{
    public Task<SecurityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        return securityPolicyRepository.GetPolicyAsync(cancellationToken);
    }

    public Task<SecurityPolicyDto> UpdatePolicyAsync(
        UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        Validate(request);

        return securityPolicyRepository.UpdatePolicyAsync(request, cancellationToken);
    }

    private static void Validate(UpdateSecurityPolicyRequest request)
    {
        EnsureRange(request.CaptchaRequiredFailures, 1, 10, "验证码触发失败次数必须在 1 到 10 之间。");
        EnsureRange(request.LockoutFailures, 1, 20, "账号锁定失败次数必须在 1 到 20 之间。");
        EnsureRange(request.LockoutMinutes, 1, 1440, "账号锁定分钟数必须在 1 到 1440 之间。");
        EnsureRange(request.CaptchaExpireSeconds, 30, 600, "验证码有效秒数必须在 30 到 600 之间。");
        EnsureRange(request.OnlineActiveTimeoutMinutes, 1, 1440, "在线活跃分钟数必须在 1 到 1440 之间。");
        EnsureRange(request.OnlineTouchThrottleSeconds, 5, 600, "在线心跳写入间隔秒数必须在 5 到 600 之间。");
        EnsureRange(request.StaleUserDays, 1, 3650, "长期未登录天数必须在 1 到 3650 之间。");
    }

    private static void EnsureRange(int value, int min, int max, string message)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(nameof(value), message);
        }
    }
}
