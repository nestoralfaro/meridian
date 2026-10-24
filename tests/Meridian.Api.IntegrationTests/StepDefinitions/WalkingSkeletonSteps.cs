using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class WalkingSkeletonSteps : IAsyncDisposable
{
    private MeridianApiFactory? _factory;
    private HttpClient? _client;
    private HttpResponseMessage? _response;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Start the app in a Given step first.");
    private HttpResponseMessage Response => _response ?? throw new InvalidOperationException("Send a request in a When step first.");

    [Given("the Meridian app is running")]
    [Given("the database is reachable")]
    public async Task GivenTheAppIsRunningWithADatabase() => Start(await MeridianApiFactory.WithDatabaseAsync());

    [Given("the database is unreachable")]
    public void GivenTheDatabaseIsUnreachable() => Start(MeridianApiFactory.WithUnreachableDatabase());

    [When(@"I (?:open|request) ""(.*)""")]
    public async Task WhenIRequest(string path)
    {
        _response?.Dispose();
        _response = await Client.GetAsync(new Uri(path, UriKind.Relative));
    }

    [Then("I receive the Meridian single-page app")]
    public async Task ThenIReceiveTheSpa()
    {
        Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpAssertions.MediaTypeOf(Response).ShouldBe("text/html");
        (await Response.Content.ReadAsStringAsync()).ShouldContain("<mer-root");
    }

    [Then(@"""(.*)"" is served from the same origin")]
    public async Task ThenIsServedFromTheSameOrigin(string path)
    {
        var pageOrigin = Response.RequestMessage!.RequestUri!.GetLeftPart(UriPartial.Authority);

        using var api = await Client.GetAsync(new Uri(path, UriKind.Relative));

        api.StatusCode.ShouldBe(HttpStatusCode.OK);
        api.RequestMessage!.RequestUri!.GetLeftPart(UriPartial.Authority).ShouldBe(pageOrigin);
    }

    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status) => ((int)Response.StatusCode).ShouldBe(status);

    [Then("the response is a problem details document")]
    public void ThenTheResponseIsProblemDetails() =>
        HttpAssertions.MediaTypeOf(Response).ShouldBe("application/problem+json");

    [Then(@"the ""(.*)"" check is reported as ""(.*)""")]
    public async Task ThenTheCheckIsReportedAs(string check, string status)
    {
        using var body = JsonDocument.Parse(await Response.Content.ReadAsStringAsync());

        var reported = body.RootElement.GetProperty("checks").EnumerateArray()
            .Single(c => c.GetProperty("name").GetString() == check);
        reported.GetProperty("status").GetString().ShouldBe(status);
    }

    [Then("the response contains the git commit SHA of the running build")]
    public async Task ThenTheResponseContainsTheCommitSha()
    {
        var expected = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion
            .Split('+').Last();

        var version = await Response.Content.ReadFromJsonAsync<JsonElement>();

        var commit = version.GetProperty("commit").GetString();
        commit.ShouldNotBeNull();
        commit.ShouldBe(expected);
        commit.ShouldMatch("^[0-9a-f]{40}$");
    }

    private void Start(MeridianApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _response?.Dispose();
        _client?.Dispose();
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }
}
