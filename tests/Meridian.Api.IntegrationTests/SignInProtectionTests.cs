using System.Net;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Meridian.Api.IntegrationTests;

public sealed class SignInProtectionTests
{
    [Fact]
    public async Task CredentialEndpointsAreRateLimitedPerClient()
    {
        await using var api = new ApiScenario();
        api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()).WithSetting("Auth:SignInAttemptsPerMinute", "3"));

        var statuses = new List<HttpStatusCode>();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            statuses.Add((await api.PostAsync("/api/v1/auth/sign-in", new { email = "someone@example.test", password = "not the right one" })).StatusCode);
        }

        statuses.ShouldBe([HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests]);
        api.LastResponse.Headers.RetryAfter.ShouldNotBeNull();
        HttpAssertions.MediaTypeOf(api.LastResponse).ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task EverySignInOutcomeIsAudited()
    {
        await using var api = new ApiScenario();
        api.Start(new MeridianApiFactory(await TestDatabase.CreateIsolatedDatabaseAsync()));
        const string email = "audited@example.test";
        const string password = "an audited passphrase";

        await api.PostAsync("/api/v1/auth/register", new { displayName = "Audit Example", email, password });
        await api.PostAsync("/api/v1/auth/sign-out", null);
        await api.PostAsync("/api/v1/auth/sign-in", new { email, password = "the wrong passphrase" });
        await api.PostAsync("/api/v1/auth/sign-in", new { email, password });

        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
        var actions = await db.AuditEvents.OrderBy(e => e.OccurredAt).Select(e => e.Action).ToListAsync(TestContext.Current.CancellationToken);
        actions.ShouldBe(["Registered", "SignedOut", "SignInFailed", "SignedIn"]);
        (await db.AuditEvents.AllAsync(e => e.CorrelationId != null, TestContext.Current.CancellationToken)).ShouldBeTrue();
    }
}
