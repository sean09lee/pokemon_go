using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace TeamBuilder.HealthChecks;

/// <summary>
/// Writes a structured JSON payload describing the result of a health check run.
/// </summary>
public static class HealthCheckResponseWriter
{
    public static Task WriteResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                durationMs = entry.Value.Duration.TotalMilliseconds,
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
