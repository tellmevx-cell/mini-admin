using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace MiniAdmin.Gateway;

public static class Program
{
    private const string DevCorsPolicy = "MiniAdminGatewayDev";

    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<GatewayRateLimitOptions>(
            builder.Configuration.GetSection(GatewayRateLimitOptions.SectionName));

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedProto;

            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(DevCorsPolicy, policy =>
            {
                policy
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .SetIsOriginAllowed(origin =>
                        origin.StartsWith("http://localhost:", StringComparison.OrdinalIgnoreCase) ||
                        origin.StartsWith("http://127.0.0.1:", StringComparison.OrdinalIgnoreCase));
            });
        });

        builder.Services.AddRateLimiter(_ => { });
        builder.Services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<GatewayRateLimitOptions>>((options, configuredOptions) =>
            {
                ConfigureRateLimiter(options, configuredOptions.Value);
            });

        builder.Services
            .AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseCors(DevCorsPolicy);
        app.UseRateLimiter();

        app.MapGet("/health", () => Results.Ok(new
        {
            Application = "MiniAdmin.Gateway",
            Status = "Healthy",
            Timestamp = DateTimeOffset.UtcNow
        }))
        .DisableRateLimiting()
        .WithName("GatewayHealthCheck");

        app.MapReverseProxy();

        app.Run();
    }

    private static void ConfigureRateLimiter(
        RateLimiterOptions options,
        GatewayRateLimitOptions rateLimitOptions)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            {
                context.HttpContext.Response.Headers.RetryAfter =
                    Math.Ceiling(retryAfter.TotalSeconds).ToString("0");
            }

            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                Code = StatusCodes.Status429TooManyRequests,
                Message = "请求过于频繁，请稍后再试。",
                Data = (object?)null
            }, cancellationToken);
        };

        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            CreateLimiter(httpContext, rateLimitOptions));
    }

    private static RateLimitPartition<string> CreateLimiter(
        HttpContext httpContext,
        GatewayRateLimitOptions options)
    {
        if (!options.Enabled ||
            httpContext.Request.Path.Equals("/health", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetNoLimiter("gateway-disabled-or-health");
        }

        var isLogin = httpContext.Request.Path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase);
        var partitionKey = CreatePartitionKey(httpContext, isLogin ? "login" : "global");

        if (isLogin)
        {
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey,
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, options.LoginPermitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, options.LoginWindowSeconds)),
                    QueueLimit = Math.Max(0, options.LoginQueueLimit),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true
                });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, options.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, options.WindowSeconds)),
                QueueLimit = Math.Max(0, options.QueueLimit),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true
            });
    }

    private static string CreatePartitionKey(HttpContext httpContext, string scope)
    {
        var authorization = httpContext.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var tokenHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(authorization))).ToLowerInvariant();
            return $"{scope}:token:{tokenHash[..16]}";
        }

        var ip = httpContext.Connection.RemoteIpAddress ?? IPAddress.None;
        return $"{scope}:ip:{ip}";
    }
}
