using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using OpenTelemetry.Trace;

namespace Meridian.Api.IntegrationTests;

public sealed partial class ObservabilityTests
{
    private const string TraceIdHeader = "X-Trace-Id";

    [Fact]
    public async Task EveryResponseCarriesItsTraceId()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/version", UriKind.Relative), TestContext.Current.CancellationToken);

        TraceIdOf(response).ShouldMatch("^[0-9a-f]{32}$");
    }

    [Fact]
    public async Task ErrorResponsesCarryTheSameTraceIdSupportCanSearchFor()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/does-not-exist", UriKind.Relative), TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        HttpAssertions.MediaTypeOf(response).ShouldBe("application/problem+json");
        problem.RootElement.GetProperty("traceId").GetString().ShouldBe(TraceIdOf(response));
    }

    [Fact]
    public async Task UnhandledExceptionsBecomeProblemDetailsWithoutLeakingInternals()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase()
            .WithTestServices(services => services.AddTransient<IStartupFilter, ThrowingEndpointFilter>());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri(ThrowingEndpointFilter.Path, UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(body);

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        HttpAssertions.MediaTypeOf(response).ShouldBe("application/problem+json");
        problem.RootElement.GetProperty("traceId").GetString().ShouldBe(TraceIdOf(response));
        body.ShouldNotContain(ThrowingEndpointFilter.SecretMessage);
        body.ShouldNotContain("at Meridian");
    }

    [Fact]
    public async Task RequestsAreTracedWithTheRouteAndTheReturnedTraceId()
    {
        var spans = new List<Activity>();
        await using var factory = MeridianApiFactory.WithUnreachableDatabase()
            .WithTestServices(services => services.ConfigureOpenTelemetryTracerProvider(t => t.AddInMemoryExporter(spans)));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/version", UriKind.Relative), TestContext.Current.CancellationToken);
        var traceId = TraceIdOf(response);
        await SettleAsync(factory, () => InTrace(spans, traceId).Any());

        var request = InTrace(spans, traceId).ShouldHaveSingleItem();
        request.DisplayName.ShouldBe("GET /api/v1/version");
        request.Kind.ShouldBe(ActivityKind.Server);
    }

    [Fact]
    public async Task HealthProbesAreNotTracedSoTheyDontConsumeTelemetryQuota()
    {
        var spans = new List<Activity>();
        await using var factory = MeridianApiFactory.WithUnreachableDatabase()
            .WithTestServices(services => services.ConfigureOpenTelemetryTracerProvider(t => t.AddInMemoryExporter(spans)));
        using var client = factory.CreateClient();

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        string[] probeTraces = [TraceIdOf(live), TraceIdOf(ready)];
        await SettleAsync(factory, () => false); // nothing should arrive; wait the full settle window

        // Neither the probe requests nor the database queries the readiness check runs are exported.
        spans.Where(s => probeTraces.Contains(s.TraceId.ToHexString())).ShouldBeEmpty();
    }

    [Fact]
    public async Task StartsAndServesWithApplicationInsightsExportEnabled()
    {
        // Bicep injects this in Azure; the exporter must not break startup or requests (it sends in the background).
        await using var factory = MeridianApiFactory.WithUnreachableDatabase()
            .WithSetting("APPLICATIONINSIGHTS_CONNECTION_STRING",
                "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://127.0.0.1:1/");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/api/v1/version", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        TraceIdOf(response).ShouldMatch("^[0-9a-f]{32}$");
    }

    [Theory]
    [InlineData("Npgsql")]
    [InlineData("Microsoft.EntityFrameworkCore.Database.Command")]
    public void DatabaseCommandsAreNotLoggedAtInformationLevel(string category)
    {
        // Readiness probes query the database every few seconds; logging each command would consume the
        // Log Analytics daily cap. Uses the production configuration (appsettings.json), not test overrides.
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"))
            .Build();
        // Like the logging system: the longest configured prefix of the category wins.
        var segments = category.Split('.');
        var level = Enumerable.Range(1, segments.Length).Reverse()
            .Select(n => configuration[$"Logging:LogLevel:{string.Join('.', segments.Take(n))}"])
            .FirstOrDefault(value => value is not null) ?? configuration["Logging:LogLevel:Default"];

        level.ShouldBe("Warning", $"'{category}' must log at Warning or above in production");
    }

    [Fact]
    public async Task LogsNeverContainCredentialsOrRequestBodies()
    {
        const string token = "s3cr3t-bearer-token";
        const string password = "hunter2-request-body";
        var logs = new FakeLogCollector();
        await using var factory = MeridianApiFactory.WithUnreachableDatabase()
            .WithTestServices(services => services.AddLogging(l => l
                .SetMinimumLevel(LogLevel.Trace)
                .AddProvider(new FakeLoggerProvider(logs))));
        using var client = factory.CreateClient();

        foreach (var path in new[] { "/api/v1/version", "/api/v1/does-not-exist", "/health/ready" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = new StringContent($$"""{"password":"{{password}}"}""", Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new("Bearer", token);
            using var _ = await client.SendAsync(request, TestContext.Current.CancellationToken);
        }

        var everything = string.Join('\n', logs.GetSnapshot().Select(r => $"{r.Message} {r.Exception}"));
        everything.ShouldNotBeEmpty();
        everything.ShouldNotContain(token);
        everything.ShouldNotContain(password);
    }

    /// <summary>
    /// Activity listeners are process-wide, so an in-memory exporter also sees spans from tests running in parallel.
    /// Assertions only consider spans that belong to this test's own requests.
    /// </summary>
    private static IEnumerable<Activity> InTrace(IEnumerable<Activity> spans, string traceId) =>
        spans.Where(s => s.TraceId.ToHexString() == traceId);

    private static string TraceIdOf(HttpResponseMessage response) =>
        response.Headers.TryGetValues(TraceIdHeader, out var values)
            ? values.Single()
            : throw new ShouldAssertException($"Expected a {TraceIdHeader} header, but the response has none.");

    /// <summary>
    /// The server span ends after the response is returned to the client, so exported spans arrive slightly later.
    /// Waits (up to 2 s) until <paramref name="done"/> is true.
    /// </summary>
    private static async Task SettleAsync(MeridianApiFactory factory, Func<bool> done)
    {
        var tracer = factory.Services.GetRequiredService<TracerProvider>();
        for (var elapsed = 0; elapsed < 2000 && !done(); elapsed += 50)
        {
            tracer.ForceFlush();
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }
    }

    /// <summary>Adds a test-only endpoint that throws, ahead of the app's pipeline.</summary>
    private sealed class ThrowingEndpointFilter : IStartupFilter
    {
        public const string Path = "/__throw.test"; // file-like: no endpoint or SPA fallback matches, so it reaches this branch
        public const string SecretMessage = "internal detail: connection to 10.0.0.5 refused";

        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(Path, branch => branch.Run(_ => throw new InvalidOperationException(SecretMessage)));
        };
    }
}
