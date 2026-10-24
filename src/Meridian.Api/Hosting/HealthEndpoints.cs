using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using static Meridian.Infrastructure.DependencyInjection;

namespace Meridian.Api.Hosting;

internal static class HealthEndpoints
{
    /// <summary>
    /// <c>/health/live</c>: the process is up (no dependency checks, so a database outage doesn't restart the app).
    /// <c>/health/ready</c>: dependencies tagged <see cref="ReadinessTag"/> are reachable (gates traffic).
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = WriteResponseAsync,
        });

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadinessTag),
            ResponseWriter = WriteResponseAsync,
        });

        return endpoints;
    }

    /// <summary>Writes status and per-check results only. Descriptions and exceptions stay in the logs.</summary>
    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";

        var body = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = Math.Round(entry.Value.Duration.TotalMilliseconds, 1),
            }),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(body, JsonSerializerOptions.Web), context.RequestAborted);
    }
}
