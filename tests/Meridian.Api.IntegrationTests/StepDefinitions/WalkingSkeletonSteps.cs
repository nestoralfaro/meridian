using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class WalkingSkeletonSteps(ApiScenario api)
{
    [Given("the Meridian app is running")]
    [Given("the database is reachable")]
    public async Task GivenTheAppIsRunningWithADatabase() => api.Start(await MeridianApiFactory.WithDatabaseAsync());

    [Given("the database is unreachable")]
    public void GivenTheDatabaseIsUnreachable() => api.Start(MeridianApiFactory.WithUnreachableDatabase());

    [When(@"I (?:open|request) ""(.*)""")]
    public Task WhenIRequest(string path) => api.GetAsync(path);

    [Then("I receive the Meridian single-page app")]
    public async Task ThenIReceiveTheSpa()
    {
        api.LastResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        HttpAssertions.MediaTypeOf(api.LastResponse).ShouldBe("text/html");
        (await api.LastResponse.Content.ReadAsStringAsync()).ShouldContain("<mer-root");
    }

    [Then(@"""(.*)"" is served from the same origin")]
    public async Task ThenIsServedFromTheSameOrigin(string path)
    {
        var pageOrigin = api.LastResponse.RequestMessage!.RequestUri!.GetLeftPart(UriPartial.Authority);

        using var response = await api.Client.GetAsync(new Uri(path, UriKind.Relative));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.RequestMessage!.RequestUri!.GetLeftPart(UriPartial.Authority).ShouldBe(pageOrigin);
    }

    [Then(@"the ""(.*)"" check is reported as ""(.*)""")]
    public async Task ThenTheCheckIsReportedAs(string check, string status)
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());

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

        var version = await api.LastResponse.Content.ReadFromJsonAsync<JsonElement>();

        var commit = version.GetProperty("commit").GetString();
        commit.ShouldNotBeNull();
        commit.ShouldBe(expected);
        commit.ShouldMatch("^[0-9a-f]{40}$");
    }
}
