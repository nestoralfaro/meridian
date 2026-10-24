using System.Reflection;

namespace Meridian.Api.Endpoints;

internal static class VersionEndpoints
{
    private static readonly VersionResponse Current = ReadBuildInfo();

    public static IEndpointRouteBuilder MapVersionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/version", () => TypedResults.Ok(Current))
            .WithName("GetVersion")
            .WithSummary("Identifies the running build (semantic version and git commit).");
        return endpoints;
    }

    /// <summary>
    /// The SDK stamps <c>AssemblyInformationalVersion</c> as <c>{version}+{commit}</c> from Source Link
    /// (or the <c>SourceRevisionId</c> build property in container builds).
    /// </summary>
    private static VersionResponse ReadBuildInfo()
    {
        var informational = typeof(VersionEndpoints).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";

        var separator = informational.IndexOf('+', StringComparison.Ordinal);
        return separator < 0
            ? new VersionResponse(informational, "unknown")
            : new VersionResponse(informational[..separator], informational[(separator + 1)..]);
    }
}

internal sealed record VersionResponse(string Version, string Commit);
