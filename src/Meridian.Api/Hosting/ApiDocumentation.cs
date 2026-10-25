using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace Meridian.Api.Hosting;

/// <summary>
/// OpenAPI document at <c>/openapi/v1.json</c> (the contract, committed as openapi/meridian-v1.json)
/// and an interactive reference at <c>/docs</c>.
/// </summary>
internal static class ApiDocumentation
{
    public static IServiceCollection AddApiDocumentation(this IServiceCollection services) =>
        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "Meridian API",
                Version = "v1",
                Description = "REST API behind the Meridian Client Portal demo. Synthetic data only; not investment advice.",
            };
            // Clients call the API on the same origin as the SPA (ADR-0004); a host-specific server URL would
            // make the committed contract differ per environment.
            document.Servers = null;
            return Task.CompletedTask;
        }));

    public static IEndpointRouteBuilder MapApiDocumentation(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOpenApi();
        endpoints.MapScalarApiReference("/docs", options => options
            .WithTitle("Meridian API")
            .WithOpenApiRoutePattern("/openapi/{documentName}.json"));
        return endpoints;
    }
}
