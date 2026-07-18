using Microsoft.AspNetCore.HttpOverrides;

namespace MiniAdmin.Api.Composition;

public static class MiniAdminForwardedHeadersExtensions
{
    private const string TrustForwardedHeadersKey = "ReverseProxy:TrustForwardedHeaders";

    public static IServiceCollection AddMiniAdminForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (!configuration.GetValue<bool>(TrustForwardedHeadersKey))
        {
            return services;
        }

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders =
                ForwardedHeaders.XForwardedFor |
                ForwardedHeaders.XForwardedHost |
                ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 2;

            // Compose binds the API to loopback and only Nginx/YARP can reach its edge network.
            // Docker network ranges are dynamic, so fixed KnownNetworks entries are not reliable.
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        return services;
    }

    public static IApplicationBuilder UseMiniAdminForwardedHeaders(
        this IApplicationBuilder app,
        IConfiguration configuration)
    {
        return configuration.GetValue<bool>(TrustForwardedHeadersKey)
            ? app.UseForwardedHeaders()
            : app;
    }
}
