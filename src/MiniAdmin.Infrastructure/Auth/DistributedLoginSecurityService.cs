using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using MiniAdmin.Application.Contracts.Auth;
using MiniAdmin.Application.Contracts.Security;
using MiniAdmin.Infrastructure.Caching;

namespace MiniAdmin.Infrastructure.Auth;

public sealed class DistributedLoginSecurityService(
    IDistributedCache cache,
    ISecurityPolicyRepository securityPolicyRepository,
    IOptions<CacheOptions> cacheOptions) : ILoginSecurityService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;

    public async Task<CaptchaDto> CreateCaptchaAsync(CancellationToken cancellationToken = default)
    {
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var id = Guid.NewGuid().ToString("N");
        var code = RandomNumberGenerator.GetInt32(1000, 10_000).ToString(CultureInfo.InvariantCulture);

        await cache.SetStringAsync(
            CaptchaKey(id),
            code,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(policy.CaptchaExpireSeconds, 30))
            },
            cancellationToken);

        return new CaptchaDto(
            id,
            CreateSvgDataUrl(code),
            Math.Max(policy.CaptchaExpireSeconds, 30));
    }

    public async Task ValidateBeforePasswordAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var state = await GetFailureStateAsync(request.Username, request.ClientIp, cancellationToken);
        if (IsLocked(state, out var remainingSeconds))
        {
            throw new LoginFailureException("登录已锁定，请稍后重试", true, remainingSeconds);
        }

        if (state.Count < Math.Max(policy.CaptchaRequiredFailures, 1))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(request.CaptchaId) ||
            string.IsNullOrWhiteSpace(request.CaptchaCode))
        {
            var failure = await RecordFailureAsync(request.Username, request.ClientIp, cancellationToken);
            throw new LoginFailureException("请输入验证码", true, failure.LockRemainingSeconds);
        }

        var key = CaptchaKey(request.CaptchaId);
        var cachedCode = await cache.GetStringAsync(key, cancellationToken);
        if (cachedCode is null ||
            !string.Equals(cachedCode, request.CaptchaCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var failure = await RecordFailureAsync(request.Username, request.ClientIp, cancellationToken);
            throw new LoginFailureException("验证码错误或已过期", true, failure.LockRemainingSeconds);
        }

        await cache.RemoveAsync(key, cancellationToken);
    }

    public async Task<LoginFailureState> RecordFailureAsync(
        string userName,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var state = await GetFailureStateAsync(userName, clientIp, cancellationToken);
        state.Count++;
        state.LastFailedAt = now;

        if (state.Count >= Math.Max(policy.LockoutFailures, 1))
        {
            state.LockedUntil = now.AddMinutes(Math.Max(policy.LockoutMinutes, 1));
        }

        await SetFailureStateAsync(userName, clientIp, state, policy, cancellationToken);
        if (state.LockedUntil is { } lockedUntil)
        {
            await SetUserLockStateAsync(userName, now, lockedUntil, cancellationToken);
        }

        return ToResult(state, policy);
    }

    public Task ClearFailuresAsync(
        string userName,
        string? clientIp,
        CancellationToken cancellationToken = default)
    {
        return cache.RemoveAsync(FailureKey(userName, clientIp), cancellationToken);
    }

    public async Task<int?> GetLockRemainingSecondsAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var cached = await cache.GetStringAsync(UserLockKey(userName), cancellationToken);
        if (string.IsNullOrWhiteSpace(cached))
        {
            return null;
        }

        var state = JsonSerializer.Deserialize<UserLockState>(cached, JsonOptions);
        if (state?.LockedUntil is null)
        {
            await cache.RemoveAsync(UserLockKey(userName), cancellationToken);
            return null;
        }

        var unlockedAt = await GetUnlockedAtAsync(userName, cancellationToken);
        if (unlockedAt is not null && state.LockedAt <= unlockedAt.Value)
        {
            await cache.RemoveAsync(UserLockKey(userName), cancellationToken);
            return null;
        }

        var remaining = state.LockedUntil - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            await cache.RemoveAsync(UserLockKey(userName), cancellationToken);
            return null;
        }

        return Math.Max((int)Math.Ceiling(remaining.TotalSeconds), 1);
    }

    public async Task UnlockUserAsync(
        string userName,
        CancellationToken cancellationToken = default)
    {
        var policy = await securityPolicyRepository.GetPolicyAsync(cancellationToken);
        await cache.SetStringAsync(
            UnlockMarkerKey(userName),
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(policy.LockoutMinutes, 1) + 1)
            },
            cancellationToken);
        await cache.RemoveAsync(UserLockKey(userName), cancellationToken);
    }

    private async Task<FailureState> GetFailureStateAsync(
        string userName,
        string? clientIp,
        CancellationToken cancellationToken)
    {
        var failureKey = FailureKey(userName, clientIp);
        var cached = await cache.GetStringAsync(failureKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(cached))
        {
            return new FailureState();
        }

        var state = JsonSerializer.Deserialize<FailureState>(cached, JsonOptions) ?? new FailureState();
        var unlockedAt = await GetUnlockedAtAsync(userName, cancellationToken);
        if (unlockedAt is not null &&
            (state.LastFailedAt is null || state.LastFailedAt.Value <= unlockedAt.Value))
        {
            await cache.RemoveAsync(failureKey, cancellationToken);
            return new FailureState();
        }

        return state;
    }

    private Task SetFailureStateAsync(
        string userName,
        string? clientIp,
        FailureState state,
        SecurityPolicyDto policy,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAfter = state.LockedUntil is { } lockedUntil
            ? lockedUntil.AddMinutes(1) - now
            : TimeSpan.FromMinutes(Math.Max(policy.LockoutMinutes, 1));

        return cache.SetStringAsync(
            FailureKey(userName, clientIp),
            JsonSerializer.Serialize(state, JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiresAfter <= TimeSpan.Zero
                    ? TimeSpan.FromMinutes(1)
                    : expiresAfter
            },
            cancellationToken);
    }

    private Task SetUserLockStateAsync(
        string userName,
        DateTimeOffset lockedAt,
        DateTimeOffset lockedUntil,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAfter = lockedUntil.AddMinutes(1) - now;

        return cache.SetStringAsync(
            UserLockKey(userName),
            JsonSerializer.Serialize(new UserLockState(lockedAt, lockedUntil), JsonOptions),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiresAfter <= TimeSpan.Zero
                    ? TimeSpan.FromMinutes(1)
                    : expiresAfter
            },
            cancellationToken);
    }

    private async Task<DateTimeOffset?> GetUnlockedAtAsync(
        string userName,
        CancellationToken cancellationToken)
    {
        var cached = await cache.GetStringAsync(UnlockMarkerKey(userName), cancellationToken);
        if (string.IsNullOrWhiteSpace(cached))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            cached,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var unlockedAt)
            ? unlockedAt
            : null;
    }

    private LoginFailureState ToResult(FailureState state, SecurityPolicyDto policy)
    {
        return new LoginFailureState(
            state.Count >= Math.Max(policy.CaptchaRequiredFailures, 1),
            IsLocked(state, out var remainingSeconds) ? remainingSeconds : null);
    }

    private static bool IsLocked(FailureState state, out int remainingSeconds)
    {
        remainingSeconds = 0;
        if (state.LockedUntil is null)
        {
            return false;
        }

        var remaining = state.LockedUntil.Value - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            return false;
        }

        remainingSeconds = Math.Max((int)Math.Ceiling(remaining.TotalSeconds), 1);
        return true;
    }

    private string CaptchaKey(string id)
    {
        return $"{NormalizePrefix()}login-security:captcha:{id.Trim()}";
    }

    private string FailureKey(string userName, string? clientIp)
    {
        return string.Concat(
            NormalizePrefix(),
            "login-security:failures:",
            NormalizePart(userName),
            ":",
            NormalizePart(clientIp ?? "unknown"));
    }

    private string UnlockMarkerKey(string userName)
    {
        return string.Concat(
            NormalizePrefix(),
            "login-security:unlock:",
            NormalizePart(userName));
    }

    private string UserLockKey(string userName)
    {
        return string.Concat(
            NormalizePrefix(),
            "login-security:user-lock:",
            NormalizePart(userName));
    }

    private string NormalizePrefix()
    {
        return string.IsNullOrWhiteSpace(_cacheOptions.KeyPrefix)
            ? "mini-admin:"
            : _cacheOptions.KeyPrefix.Trim();
    }

    private static string NormalizePart(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return string.IsNullOrWhiteSpace(normalized)
            ? "empty"
            : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    private static string CreateSvgDataUrl(string code)
    {
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" width="120" height="40" viewBox="0 0 120 40">
              <rect width="120" height="40" rx="4" fill="#f5f7fb"/>
              <path d="M8 30 C24 10 40 35 58 16 S92 18 112 8" stroke="#8b5cf6" stroke-width="2" fill="none" opacity="0.45"/>
              <path d="M6 12 L112 32" stroke="#06b6d4" stroke-width="1.5" opacity="0.35"/>
              <text x="60" y="27" text-anchor="middle" font-family="Consolas, monospace" font-size="24" font-weight="700" letter-spacing="6" fill="#1f2937">{{code}}</text>
            </svg>
            """;

        return $"data:image/svg+xml;base64,{Convert.ToBase64String(Encoding.UTF8.GetBytes(svg))}";
    }

    private sealed class FailureState
    {
        public int Count { get; set; }

        public DateTimeOffset? LastFailedAt { get; set; }

        public DateTimeOffset? LockedUntil { get; set; }
    }

    private sealed record UserLockState(DateTimeOffset LockedAt, DateTimeOffset LockedUntil);
}
