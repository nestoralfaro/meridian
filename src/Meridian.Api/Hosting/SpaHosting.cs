using Microsoft.Net.Http.Headers;

namespace Meridian.Api.Hosting;

/// <summary>
/// Serves the Angular build from the web root on the same origin as the API (ADR-0004 single origin):
/// static assets, client-side route fallback to <c>index.html</c>, and reserved prefixes that never fall back.
/// </summary>
internal static class SpaHosting
{
    private const string IndexFile = "index.html";

    /// <summary>Path prefixes owned by the server. Unknown paths under them return 404, never the SPA.</summary>
    private static readonly string[] ReservedPrefixes = ["/api", "/health"];

    private static readonly StaticFileOptions StaticFiles = new()
    {
        OnPrepareResponse = context =>
        {
            // index.html references hashed bundles; it must be revalidated so a new deployment is picked up immediately.
            if (string.Equals(context.File.Name, IndexFile, StringComparison.OrdinalIgnoreCase))
            {
                context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
            }
        },
    };

    public static IApplicationBuilder UseSpaStaticFiles(this IApplicationBuilder app) => app.UseStaticFiles(StaticFiles);

    public static IEndpointRouteBuilder MapSpaFallback(this IEndpointRouteBuilder endpoints)
    {
        foreach (var prefix in ReservedPrefixes)
        {
            endpoints.MapGroup(prefix).MapFallback(() => Results.NotFound());
        }

        // Default pattern {*path:nonfile}: paths that look like files (e.g. /missing.js) are not rewritten to the SPA.
        endpoints.MapFallbackToFile(IndexFile, StaticFiles);
        return endpoints;
    }
}
