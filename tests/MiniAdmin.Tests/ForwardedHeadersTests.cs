using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using MiniAdmin.Api.Composition;

namespace MiniAdmin.Tests;

public sealed class ForwardedHeadersTests
{
    [Fact]
    public async Task Trusted_proxy_headers_restore_public_https_request_data()
    {
        var payload = await GetRequestInfoAsync(trustForwardedHeaders: true);

        Assert.Contains("\"scheme\":\"https\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"host\":\"admin.example.com\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"remoteIpAddress\":\"203.0.113.10\"", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Proxy_headers_are_ignored_unless_the_proxy_chain_is_trusted()
    {
        var payload = await GetRequestInfoAsync(trustForwardedHeaders: false);

        Assert.Contains("\"scheme\":\"http\"", payload, StringComparison.Ordinal);
        Assert.Contains("\"host\":\"localhost\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.10", payload, StringComparison.Ordinal);
    }

    private static async Task<string> GetRequestInfoAsync(bool trustForwardedHeaders)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReverseProxy:TrustForwardedHeaders"] = trustForwardedHeaders.ToString()
            })
            .Build();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddMiniAdminForwardedHeaders(configuration);

        await using var app = builder.Build();
        app.UseMiniAdminForwardedHeaders(configuration);
        app.MapGet("/request-info", (HttpContext context) => Results.Json(new
        {
            context.Request.Scheme,
            Host = context.Request.Host.Value,
            RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString()
        }));
        await app.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/request-info");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        request.Headers.TryAddWithoutValidation("X-Forwarded-Host", "admin.example.com");
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.10");
        using var response = await app.GetTestClient().SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        return payload;
    }
}
