using System.Net;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;

namespace Meridian.Api.IntegrationTests;

/// <summary>A revision whose migrations weren't applied must never become ready (so it never receives traffic).</summary>
public sealed class SchemaReadinessTests
{
    [Fact]
    public async Task ReadyWhenEveryMigrationIsApplied()
    {
        await using var factory = await MeridianApiFactory.WithDatabaseAsync();

        var (status, checks) = await GetReadinessAsync(factory);

        status.ShouldBe(HttpStatusCode.OK);
        checks["schema"].ShouldBe("Healthy");
    }

    [Fact]
    public async Task NotReadyWhileMigrationsArePending()
    {
        await using var factory = await MeridianApiFactory.WithUnmigratedDatabaseAsync();

        var (status, checks) = await GetReadinessAsync(factory);

        status.ShouldBe(HttpStatusCode.ServiceUnavailable);
        checks["database"].ShouldBe("Healthy");
        checks["schema"].ShouldBe("Unhealthy");
    }

    private static async Task<(HttpStatusCode Status, Dictionary<string, string> Checks)> GetReadinessAsync(MeridianApiFactory factory)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var checks = body.RootElement.GetProperty("checks").EnumerateArray()
            .ToDictionary(c => c.GetProperty("name").GetString()!, c => c.GetProperty("status").GetString()!);
        return (response.StatusCode, checks);
    }
}
