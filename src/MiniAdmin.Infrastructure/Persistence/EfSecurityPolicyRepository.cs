using System.Globalization;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Parameters;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Infrastructure.Auth;

namespace MiniAdmin.Infrastructure.Persistence;

public sealed class EfSecurityPolicyRepository(
    ISystemParameterRepository systemParameterRepository,
    IOptions<LoginSecurityOptions> loginSecurityOptions,
    IOptions<OnlineUserOptions> onlineUserOptions) : ISecurityPolicyRepository
{
    private const string Group = "security";
    private const int StaleUserDaysDefault = 90;

    private static readonly SecurityPolicyParameter[] Parameters =
    [
        new("security.login.captcha_required_failures", "验证码触发失败次数", 6, "同一用户/IP 连续登录失败达到该次数后要求验证码"),
        new("security.login.lockout_failures", "账号锁定失败次数", 7, "连续登录失败达到该次数后锁定登录"),
        new("security.login.lockout_minutes", "账号锁定分钟数", 8, "登录锁定持续时间"),
        new("security.login.captcha_expire_seconds", "验证码有效秒数", 9, "验证码缓存有效期"),
        new("security.online.active_timeout_minutes", "在线活跃分钟数", 10, "超过该时间无请求则不视为在线"),
        new("security.online.touch_throttle_seconds", "在线心跳写入间隔秒数", 11, "降低每次请求刷新在线状态的写库频率"),
        new("security.account.stale_user_days", "长期未登录天数", 12, "安全中心统计长期未登录用户的阈值")
    ];

    public async Task<SecurityPolicyDto> GetPolicyAsync(CancellationToken cancellationToken = default)
    {
        var loginDefaults = loginSecurityOptions.Value;
        var onlineDefaults = onlineUserOptions.Value;

        return new SecurityPolicyDto(
            await GetIntAsync(Parameters[0].Key, loginDefaults.CaptchaRequiredFailures, 1, 10, cancellationToken),
            await GetIntAsync(Parameters[1].Key, loginDefaults.LockoutFailures, 1, 20, cancellationToken),
            await GetIntAsync(Parameters[2].Key, loginDefaults.LockoutMinutes, 1, 1440, cancellationToken),
            await GetIntAsync(Parameters[3].Key, loginDefaults.CaptchaExpireSeconds, 30, 600, cancellationToken),
            await GetIntAsync(Parameters[4].Key, onlineDefaults.ActiveTimeoutMinutes, 1, 1440, cancellationToken),
            await GetIntAsync(Parameters[5].Key, onlineDefaults.TouchThrottleSeconds, 5, 600, cancellationToken),
            await GetIntAsync(Parameters[6].Key, StaleUserDaysDefault, 1, 3650, cancellationToken));
    }

    public async Task<SecurityPolicyDto> UpdatePolicyAsync(
        UpdateSecurityPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        var values = new[]
        {
            request.CaptchaRequiredFailures,
            request.LockoutFailures,
            request.LockoutMinutes,
            request.CaptchaExpireSeconds,
            request.OnlineActiveTimeoutMinutes,
            request.OnlineTouchThrottleSeconds,
            request.StaleUserDays
        };

        for (var index = 0; index < Parameters.Length; index++)
        {
            var parameter = Parameters[index];
            await systemParameterRepository.UpsertValueByKeyAsync(
                parameter.Key,
                parameter.Name,
                values[index].ToString(CultureInfo.InvariantCulture),
                Group,
                parameter.Remark,
                parameter.Order,
                true,
                cancellationToken);
        }

        return await GetPolicyAsync(cancellationToken);
    }

    private async Task<int> GetIntAsync(
        string key,
        int defaultValue,
        int min,
        int max,
        CancellationToken cancellationToken)
    {
        var rawValue = await systemParameterRepository.GetValueByKeyAsync(key, cancellationToken);
        if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return defaultValue;
        }

        return value < min || value > max ? defaultValue : value;
    }

    private sealed record SecurityPolicyParameter(
        string Key,
        string Name,
        int Order,
        string Remark);
}
