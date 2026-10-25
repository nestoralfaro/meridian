using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Meridian.Api.IntegrationTests.Infrastructure;

namespace Meridian.Api.IntegrationTests;

/// <summary>
/// The OpenAPI document is the contract between the API and the SPA (and any other client).
/// It is committed at openapi/meridian-v1.json so changes show up in code review; CI regenerates the
/// TypeScript types from it and checks for breaking changes against main (oasdiff).
/// </summary>
public sealed class OpenApiContractTests
{
    private const string UpdateVariable = "MERIDIAN_UPDATE_OPENAPI";
    private static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true };

    [Fact]
    public async Task TheCommittedContractMatchesTheApi()
    {
        var live = await GetLiveDocumentAsync();
        var path = Path.Combine(RepositoryRoot(), "openapi", "meridian-v1.json");

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, live.ToJsonString(Pretty) + "\n", TestContext.Current.CancellationToken);
        }

        File.Exists(path).ShouldBeTrue($"Missing {path}. Generate it with {UpdateVariable}=1 dotnet test.");
        var committed = JsonNode.Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        JsonNode.DeepEquals(committed, live).ShouldBeTrue(
            $"openapi/meridian-v1.json is out of date. Regenerate with {UpdateVariable}=1 dotnet test, then run `npm run api:generate` in src/meridian-web.");
    }

    [Fact]
    public async Task TheContractDescribesTheVersionedApiWithoutEnvironmentSpecificServers()
    {
        var document = await GetLiveDocumentAsync();

        document["info"]!["title"]!.GetValue<string>().ShouldBe("Meridian API");
        document["paths"]!.AsObject().ContainsKey("/api/v1/version").ShouldBeTrue();
        document["components"]!["schemas"]!.AsObject().ContainsKey("VersionResponse").ShouldBeTrue();
        document.AsObject().ContainsKey("servers").ShouldBeFalse(); // relative: the SPA calls the same origin
    }

    [Fact]
    public async Task InteractiveApiReferenceIsServedAtDocs()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/docs", UriKind.Relative), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpAssertions.MediaTypeOf(response).ShouldBe("text/html");
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("openapi/v1.json");
    }

    private static async Task<JsonNode> GetLiveDocumentAsync()
    {
        await using var factory = MeridianApiFactory.WithUnreachableDatabase();
        using var client = factory.CreateClient();
        var json = await client.GetStringAsync(new Uri("/openapi/v1.json", UriKind.Relative), TestContext.Current.CancellationToken);
        return JsonNode.Parse(json)!;
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Meridian.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Repository root (Meridian.slnx) not found.");
    }
}
