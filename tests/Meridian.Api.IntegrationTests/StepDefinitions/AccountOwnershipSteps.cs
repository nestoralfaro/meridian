using System.Text.Json;
using System.Text.Json.Nodes;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class AccountOwnershipSteps(ApiScenario api)
{
    private string? _firstBody;

    [When(@"I request (Dana|Raj)'s ""(.*)"" by its ID")]
    public async Task WhenIRequestAnAccount(string persona, string accountName)
    {
        var email = persona == "Dana" ? DemoPortfolio.DanaEmail : DemoPortfolio.RajEmail;
        var accountId = await UseDbAsync(async db =>
        {
            var client = await db.Clients.SingleAsync(c => c.Email == email);
            return await db.Accounts.Where(a => a.ClientId == client.Id && a.Name == accountName).Select(a => a.Id).SingleAsync();
        });
        await api.GetAsync($"/api/v1/accounts/{accountId}");
        _firstBody = await api.LastResponse.Content.ReadAsStringAsync();
    }

    [When("I request an account ID that doesn't exist")]
    public Task WhenIRequestAMissingAccount() => api.GetAsync($"/api/v1/accounts/{Guid.NewGuid()}");

    [Then(@"an ""(.*)"" audit event is recorded for Dana")]
    public async Task ThenAnAuditEventIsRecorded(string action)
    {
        var danaUserId = await UseDbAsync(db => db.Users.Where(u => u.Email == DemoPortfolio.DanaEmail).Select(u => u.Id).SingleAsync());
        var recorded = await UseDbAsync(db => db.AuditEvents.AnyAsync(e => e.Action == action && e.Actor == $"user:{danaUserId}"));
        recorded.ShouldBeTrue();
    }

    [Then("both answers are identical apart from the trace id")]
    public async Task ThenBothAnswersAreIdentical()
    {
        static JsonNode WithoutTraceId(string json)
        {
            var node = JsonNode.Parse(json)!;
            node.AsObject().Remove("traceId");
            return node;
        }

        var second = await api.LastResponse.Content.ReadAsStringAsync();
        JsonNode.DeepEquals(WithoutTraceId(_firstBody!), WithoutTraceId(second)).ShouldBeTrue($"{_firstBody}\n{second}");
    }

    [Then(@"the account is worth ""(.*)""")]
    public async Task ThenTheAccountIsWorth(string amount)
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("totalValue").GetProperty("amount").GetDecimal()
            .ShouldBe(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task<T> UseDbAsync<T>(Func<MeridianDbContext, Task<T>> query)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<MeridianDbContext>());
    }
}
