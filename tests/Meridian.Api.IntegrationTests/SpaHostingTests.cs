using System.Net;
using Meridian.Api.IntegrationTests.Infrastructure;

namespace Meridian.Api.IntegrationTests;

/// <summary>Details of serving the Angular build that sit below the acceptance scenarios.</summary>
public sealed class SpaHostingTests : IAsyncLifetime
{
    private MeridianApiFactory _factory = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await MeridianApiFactory.WithDatabaseAsync();
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    public async Task IndexHtmlIsNeverCachedSoNewDeploymentsAreSeenImmediately(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpAssertions.CacheControlOf(response).NoCache.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingStaticFilesReturn404InsteadOfTheSpa()
    {
        using var response = await _client.GetAsync(new Uri("/main-DOESNOTEXIST.js", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/api")]
    [InlineData("/api/v2/version")]
    [InlineData("/health")]
    [InlineData("/health/unknown")]
    public async Task ReservedPrefixesAreNeverServedTheSpa(string path)
    {
        using var response = await _client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        HttpAssertions.MediaTypeOf(response).ShouldBe("application/problem+json");
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }
}
