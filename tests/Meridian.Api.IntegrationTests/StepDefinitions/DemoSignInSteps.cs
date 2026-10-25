using System.Net;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Application.DemoData;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class DemoSignInSteps(ApiScenario api)
{
    private const string SessionCookie = "meridian.session";

    [Given("the demo personas exist")]
    public async Task GivenTheDemoPersonasExist()
    {
        api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()));
        await using var scope = api.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IDemoData>().EnsureSeededAsync();
    }

    [When(@"I sign in as the demo persona ""(.*)""")]
    [Given(@"I am signed in as the demo persona ""(.*)""")]
    public Task WhenISignInAsTheDemoPersona(string persona) =>
        api.PostAsync("/api/v1/auth/demo-sign-in", new { persona });

    [When(@"I sign in as the demo persona ""(.*)"" without an antiforgery token")]
    public Task WhenISignInWithoutAntiforgery(string persona) =>
        api.PostAsync("/api/v1/auth/demo-sign-in", new { persona }, withAntiforgeryToken: false);

    [When("I try to change the password")]
    public Task WhenITryToChangeThePassword() =>
        api.PostAsync("/api/v1/auth/change-password", new { currentPassword = "anything", newPassword = "a-new-long-passphrase" });

    [Then(@"I am signed in as ""(.*)""")]
    public async Task ThenIAmSignedInAs(string displayName)
    {
        api.LastResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var me = await api.Client.GetAsync(new Uri("/api/v1/auth/me", UriKind.Relative));
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await me.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("displayName").GetString().ShouldBe(displayName);
        body.RootElement.GetProperty("isDemo").GetBoolean().ShouldBeTrue();
    }

    [Then("the session cookie is HttpOnly, Secure and SameSite=Strict")]
    public void ThenTheSessionCookieIsHardened()
    {
        var cookie = api.LastResponse.Headers.GetValues("Set-Cookie")
            .Single(c => c.StartsWith($"{SessionCookie}=", StringComparison.Ordinal))
            .ToLowerInvariant();

        cookie.ShouldContain("; httponly");
        cookie.ShouldContain("; secure");
        cookie.ShouldContain("; samesite=strict");
    }

    [Then("the response body contains no access token")]
    public async Task ThenTheResponseBodyContainsNoToken()
    {
        var body = await api.LastResponse.Content.ReadAsStringAsync();
        body.ShouldBeEmpty();
    }

    [Then(@"the problem detail reads ""(.*)""")]
    public async Task ThenTheProblemDetailReads(string detail)
    {
        HttpAssertions.MediaTypeOf(api.LastResponse).ShouldBe("application/problem+json");
        using var problem = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("detail").GetString().ShouldBe(detail);
    }
}
