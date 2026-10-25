using System.Net;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Application.DemoData;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Api.IntegrationTests;

public sealed class ActivityEndpointTests
{
    [Fact]
    public async Task ReturnsTheSignedInClientsNewestTransactionsFirst()
    {
        await using var api = await SignedInAsDanaAsync();

        using var response = await api.GetAsync("/api/v1/activity");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dates = body.RootElement.GetProperty("items").EnumerateArray().Select(i => DateOnly.Parse(i.GetProperty("date").GetString()!, System.Globalization.CultureInfo.InvariantCulture)).ToList();
        dates.Count.ShouldBe(5);
        dates.ShouldBe(dates.OrderByDescending(d => d).ToList());
        body.RootElement.GetProperty("items").EnumerateArray().ShouldAllBe(i => i.GetProperty("accountName").GetString() == "Individual Brokerage" || i.GetProperty("accountName").GetString() == "Roth IRA");
    }

    [Fact]
    public async Task HonoursTheRequestedLimit()
    {
        await using var api = await SignedInAsDanaAsync();

        using var response = await api.GetAsync("/api/v1/activity?limit=2");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        body.RootElement.GetProperty("items").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task RequiresSignIn()
    {
        await using var api = new ApiScenario();
        api.Start(MeridianApiFactory.WithUnreachableDatabase());

        (await api.GetAsync("/api/v1/activity")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static async Task<ApiScenario> SignedInAsDanaAsync()
    {
        var api = new ApiScenario();
        api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()));
        await using (var scope = api.Factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IDemoData>().EnsureSeededAsync();
        }

        (await api.PostAsync("/api/v1/auth/demo-sign-in", new { persona = "dana" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return api;
    }
}
