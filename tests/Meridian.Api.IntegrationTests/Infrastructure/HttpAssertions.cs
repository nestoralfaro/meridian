using System.Net.Http.Headers;

namespace Meridian.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Header accessors that fail when the header is missing. Writing <c>header?.Value.ShouldBe(x)</c>
/// silently skips the assertion when the header is absent; these helpers make that impossible.
/// </summary>
internal static class HttpAssertions
{
    public static CacheControlHeaderValue CacheControlOf(HttpResponseMessage response) =>
        response.Headers.CacheControl ?? throw new ShouldAssertException("Expected a Cache-Control header, but the response has none.");

    public static string MediaTypeOf(HttpResponseMessage response) =>
        response.Content.Headers.ContentType?.MediaType ?? throw new ShouldAssertException("Expected a Content-Type header, but the response has none.");
}
