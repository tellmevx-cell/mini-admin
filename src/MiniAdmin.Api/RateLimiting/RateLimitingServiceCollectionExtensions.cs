using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using MiniAdmin.Shared;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MiniAdmin.Api.RateLimiting;

public static class RateLimitingServiceCollectionExtensions
{
    public static IServiceCollection AddMiniAdminRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<MiniAdminRateLimitOptions>(
            configuration.GetSection(MiniAdminRateLimitOptions.SectionName));

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<MiniAdminRateLimitOptions>>((options, configuredOptions) =>
            {
                ConfigureRateLimiter(options, configuredOptions.Value);
            });

        return services;
    }

    private static void ConfigureRateLimiter(
        RateLimiterOptions options,
        MiniAdminRateLimitOptions rateLimitOptions)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            var response = context.HttpContext.Response;
            response.StatusCode = StatusCodes.Status429TooManyRequests;

            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                    .ToString(CultureInfo.InvariantCulture);
            }

            await response.WriteAsJsonAsync(
                ApiResponse<object?>.Fail("请求过于频繁，请稍后再试。", code: 429),
                cancellationToken);
        };

        options.GlobalLimiter = CreateGlobalLimiter(rateLimitOptions);
        options.AddPolicy(
            MiniAdminRateLimitPolicyNames.Login,
            CreateLoginPolicy(rateLimitOptions));
        options.AddPolicy(
            MiniAdminRateLimitPolicyNames.Upload,
            CreateUploadPolicy(rateLimitOptions));
    }

    private static PartitionedRateLimiter<HttpContext> CreateGlobalLimiter(
        MiniAdminRateLimitOptions options)
    {
        return PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            if (!options.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("global-disabled");
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                GetPartitionKey(httpContext, "global"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Positive(options.PermitLimit, 600),
                    Window = TimeSpan.FromSeconds(Positive(options.WindowSeconds, 60)),
                    QueueLimit = Math.Max(0, options.QueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        });
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateLoginPolicy(
        MiniAdminRateLimitOptions options)
    {
        return httpContext =>
        {
            if (!options.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("login-disabled");
            }

            return RateLimitPartition.GetFixedWindowLimiter(
                GetIpPartitionKey(httpContext, "login"),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Positive(options.LoginPermitLimit, 10),
                    Window = TimeSpan.FromSeconds(Positive(options.LoginWindowSeconds, 60)),
                    QueueLimit = Math.Max(0, options.LoginQueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        };
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateUploadPolicy(
        MiniAdminRateLimitOptions options)
    {
        return httpContext =>
        {
            if (!options.Enabled)
            {
                return RateLimitPartition.GetNoLimiter("upload-disabled");
            }

            return RateLimitPartition.GetConcurrencyLimiter(
                GetPartitionKey(httpContext, "upload"),
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = Positive(options.UploadPermitLimit, 4),
                    QueueLimit = Math.Max(0, options.UploadQueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                });
        };
    }

    private static string GetPartitionKey(HttpContext httpContext, string scope)
    {
        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId)
            ? $"{scope}:user:{userId}"
            : GetIpPartitionKey(httpContext, scope);
    }

    private static string GetIpPartitionKey(HttpContext httpContext, string scope)
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
        return $"{scope}:ip:{(string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp)}";
    }

    private static int Positive(int value, int fallback)
    {
        return value > 0 ? value : fallback;
    }
}
