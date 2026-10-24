using System.Net;
using Meridian.Api.IntegrationTests.Infrastructure;

namespace Meridian.Api.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task UnhealthyResponsesDoNotLeakConnectionOrExceptionDetails()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        body.ShouldNotContain("127.0.0.1");
        body.ShouldNotContain("Exception", Case.Insensitive);
        body.ShouldNotContain("nobody");
    }

    [Fact]
    public async Task HealthResponsesAreJsonAndNeverCached()
    {
        await using var factory = await MeridianApiFactory.WithDatabaseAsync();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/ready", UriKind.Relative), TestContext.Current.CancellationToken);

        HttpAssertions.MediaTypeOf(response).ShouldBe("application/json");
        HttpAssertions.CacheControlOf(response).NoStore.ShouldBeTrue();
    }
}
