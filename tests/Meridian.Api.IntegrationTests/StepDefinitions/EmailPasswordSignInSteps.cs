using System.Net;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Application.DemoData;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class EmailPasswordSignInSteps(ApiScenario api)
{
    /// <summary>The single message every failed sign-in gets (wrong password, unknown email, locked, demo account).</summary>
    private const string GenericFailure = "Sign-in failed. Check your email and password, or try again later.";

    [Given("the Meridian app is running with a fresh database")]
    public async Task GivenAFreshDatabase() => api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()));

    [When(@"I register as ""(.*)"" with a unique email and the password ""(.*)""")]
    public Task WhenIRegister(string displayName, string password) =>
        api.PostAsync("/api/v1/auth/register", new { displayName, email = $"avery.{Guid.NewGuid():N}@example.test", password });

    [Given(@"an account exists for ""(.*)"" with the password ""(.*)""")]
    public async Task GivenAnAccountExists(string email, string password)
    {
        var registered = await api.PostAsync("/api/v1/auth/register", new { displayName = "Raj Example", email, password });
        registered.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await api.PostAsync("/api/v1/auth/sign-out", null);
    }

    [When(@"I sign in as ""(.*)"" with the password ""(.*)""")]
    public Task WhenISignIn(string email, string password) =>
        api.PostAsync("/api/v1/auth/sign-in", new { email, password });

    [When(@"(\d+) sign-in attempts for ""(.*)"" fail in a row")]
    public async Task WhenAttemptsFail(int attempts, string email)
    {
        for (var i = 0; i < attempts; i++)
        {
            var response = await api.PostAsync("/api/v1/auth/sign-in", new { email, password = $"wrong password number {i}" });
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    [When("a sign-in request is sent without a valid antiforgery token")]
    public Task WhenSentWithoutAntiforgery() =>
        api.PostAsync("/api/v1/auth/sign-in", new { email = "raj@example.test", password = "whatever it is" }, withAntiforgeryToken: false);

    [Given("the demo personas exist")]
    public async Task GivenTheDemoPersonasExist()
    {
        api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()));
        await using var scope = api.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDemoData>().EnsureSeededAsync();
    }

    [Then("my account is created")]
    public void ThenMyAccountIsCreated() => api.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

    [Then(@"the ""(.*)"" field reports ""(.*)""")]
    public async Task ThenTheFieldReports(string field, string message)
    {
        HttpAssertions.MediaTypeOf(api.LastResponse).ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString());
        errors.ShouldContain(message);
    }

    [Then("the sign-in failure message does not reveal whether the email exists")]
    public async Task ThenTheMessageIsGeneric()
    {
        using var problem = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString().ShouldBe(GenericFailure);
    }
}
