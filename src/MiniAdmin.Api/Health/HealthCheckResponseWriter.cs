using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MiniAdmin.Api.Health;

public static class HealthCheckResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        var payload = new
        {
            Application = "MiniAdmin.Api",
            Status = report.Status.ToString(),
            TotalDurationMs = Math.Round(report.TotalDuration.TotalMilliseconds, 2),
            Timestamp = DateTimeOffset.UtcNow,
            Checks = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    Status = entry.Value.Status.ToString(),
                    DurationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 2),
                    entry.Value.Description
                })
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
