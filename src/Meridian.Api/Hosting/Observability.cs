using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Npgsql;
using OpenTelemetry.Instrumentation.AspNetCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Meridian.Api.Hosting;

/// <summary>
/// OpenTelemetry traces, metrics and logs; exported to Application Insights when
/// <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> is set (injected by Bicep), otherwise collected but not exported.
/// Every response carries its W3C trace id (<see cref="TraceIdHeader"/>, and <c>traceId</c> in ProblemDetails),
/// which is the <c>operation_Id</c> to search for in Application Insights.
/// </summary>
internal static class Observability
{
    public const string TraceIdHeader = "X-Trace-Id";
    private const string ServiceName = "meridian-api";

    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(ServiceName, serviceVersion: typeof(Observability).Assembly.GetName().Version?.ToString())
                .AddAttributes([new("deployment.environment.name", builder.Environment.EnvironmentName)]))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Npgsql"));

        // Health probes run every few seconds; tracing them would only consume telemetry quota.
        builder.Services.Configure<AspNetCoreTraceInstrumentationOptions>(options =>
            options.Filter = context => !context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            telemetry.UseAzureMonitor();
        }

        builder.Services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = TraceIdOf(context.HttpContext));

        return builder;
    }

    /// <summary>Adds the trace id to every response, including errors and static files.</summary>
    public static IApplicationBuilder UseTraceIdHeader(this IApplicationBuilder app) => app.Use((context, next) =>
    {
        var traceId = TraceIdOf(context);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[TraceIdHeader] = traceId;
            return Task.CompletedTask;
        });
        return next(context);
    });

    private static string TraceIdOf(HttpContext context) =>
        Activity.Current?.TraceId.ToHexString() ?? context.TraceIdentifier;
}
