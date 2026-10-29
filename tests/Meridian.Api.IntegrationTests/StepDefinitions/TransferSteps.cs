using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class TransferSteps(ApiScenario api) : IDisposable
{
    private const string IdempotencyKey = "Idempotency-Key";
    private (object Body, string Key)? _last;
    private uint _reviewedVersion;
    private List<HttpResponseMessage> _concurrent = [];

    [When(@"I transfer ""(.*)"" from ""(.*)"" to ""(.*)"" with idempotency key ""(.*)""")]
    public async Task WhenITransfer(string amount, string from, string to, string key) =>
        await SendAsync(await BodyAsync(amount, from, DemoPortfolio.DanaEmail, to), key);

    [When(@"I transfer ""(.*)"" from ""(.*)"" to Raj's ""(.*)"" with idempotency key ""(.*)""")]
    public async Task WhenITransferToRaj(string amount, string from, string to, string key) =>
        await SendAsync(await BodyAsync(amount, from, DemoPortfolio.RajEmail, to), key);

    [When("I send exactly the same request again")]
    public async Task WhenISendTheSameRequestAgain() => await SendAsync(_last!.Value.Body, _last.Value.Key);

    [When(@"I transfer ""(.*)"" from ""(.*)"" to ""(.*)"" without an idempotency key")]
    public async Task WhenITransferWithoutKey(string amount, string from, string to) =>
        await SendAsync(await BodyAsync(amount, from, DemoPortfolio.DanaEmail, to), key: null);

    [When(@"I transfer ""(.*)"" from ""(.*)"" to ""(.*)"" without an antiforgery token")]
    public async Task WhenITransferWithoutAntiforgery(string amount, string from, string to)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers")
        {
            Content = JsonContent.Create(await BodyAsync(amount, from, DemoPortfolio.DanaEmail, to)),
        };
        request.Headers.Add(IdempotencyKey, "no-xsrf");
        await api.SendAsync(request);
    }

    [When(@"(\d+) identical transfers of ""(.*)"" with idempotency key ""(.*)"" arrive at the same time")]
    public async Task WhenIdenticalTransfersArriveTogether(int count, string amount, string key)
    {
        var body = await BodyAsync(amount, "Individual Brokerage", DemoPortfolio.DanaEmail, "Roth IRA");
        var token = await api.GetAntiforgeryTokenAsync();
        _concurrent = [.. await Task.WhenAll(Enumerable.Range(0, count).Select(_ => PostRawAsync(body, key, token)))];
    }

    [When(@"(\d+) transfers of ""(.*)"" from ""(.*)"" with different keys arrive at the same time")]
    public async Task WhenDifferentTransfersArriveTogether(int count, string amount, string from)
    {
        var body = await BodyAsync(amount, from, DemoPortfolio.DanaEmail, "Roth IRA");
        var token = await api.GetAntiforgeryTokenAsync();
        _concurrent = [.. await Task.WhenAll(Enumerable.Range(0, count).Select(i => PostRawAsync(body, $"k-parallel-{i}", token)))];
    }

    [Given(@"another transfer changed ""(.*)"" after I reviewed it")]
    public async Task GivenAnotherTransferChangedTheAccount(string account)
    {
        _reviewedVersion = await VersionOfAsync(account);
        await SendAsync(await BodyAsync("1.00", account, DemoPortfolio.DanaEmail, "Roth IRA"), "someone-else");
        ((int)api.LastResponse.StatusCode).ShouldBe(201);
    }

    [When(@"I confirm my reviewed transfer of ""(.*)"" from ""(.*)"" to ""(.*)""")]
    public async Task WhenIConfirmMyReviewedTransfer(string amount, string from, string to)
    {
        var body = await BodyAsync(amount, from, DemoPortfolio.DanaEmail, to, version: _reviewedVersion);
        await SendAsync(body, "my-review");
    }

    [Then(@"""(.*)"" has ""(.*)"" in cash")]
    public async Task ThenTheAccountHasCash(string account, string amount) =>
        (await UseDbAsync(db => DanasAccounts(db).Where(a => a.Name == account).Select(a => a.Cash.Amount).SingleAsync()))
            .ShouldBe(Parse(amount));

    [Then(@"""(.*)"" activity shows ""(.*)"" for ""(.*)""")]
    public async Task ThenActivityShows(string account, string description, string amount)
    {
        var found = await UseDbAsync(db => db.Transactions.AnyAsync(t =>
            DanasAccounts(db).Where(a => a.Name == account).Select(a => a.Id).Contains(t.AccountId)
            && t.Description == description && t.Amount.Amount == Parse(amount)));
        found.ShouldBeTrue();
    }

    [Then(@"a ""(.*)"" audit event is recorded")]
    public async Task ThenAnAuditEventIsRecorded(string action) =>
        (await UseDbAsync(db => db.AuditEvents.AnyAsync(e => e.Action == action && e.Subject.StartsWith("transfer:")))).ShouldBeTrue();

    [Then("the response says it was replayed")]
    public void ThenTheResponseWasReplayed() =>
        api.LastResponse.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);

    [Then(@"the problem says ""(.*)"" is available")]
    public async Task ThenTheProblemSaysAvailable(string amount) =>
        (await api.LastResponse.Content.ReadAsStringAsync()).ShouldContain(amount);

    [Then("every response is 201 or 200 and they all name the same transfer")]
    public async Task ThenEveryResponseNamesTheSameTransfer()
    {
        var ids = new List<string>();
        foreach (var response in _concurrent)
        {
            var body = await response.Content.ReadAsStringAsync();
            ((int)response.StatusCode).ShouldBeOneOf([201, 200], body);
            using var json = JsonDocument.Parse(body);
            ids.Add(json.RootElement.GetProperty("id").GetString()!);
        }

        ids.Distinct().ShouldHaveSingleItem();
        _concurrent.Count(r => (int)r.StatusCode == 201).ShouldBe(1);
    }

    [Then(@"no more than (\d+) succeed")]
    public void ThenNoMoreThanSucceed(int max)
    {
        var statuses = _concurrent.Select(r => (int)r.StatusCode).ToList();
        statuses.ShouldAllBe(s => s == 201 || s == 409 || s == 422, string.Join(", ", statuses));
        statuses.Count(s => s == 201).ShouldBeInRange(1, max);
    }

    [Then(@"""(.*)"" never has less than ""(.*)"" in cash")]
    public async Task ThenNeverLessThan(string account, string minimum)
    {
        var succeeded = _concurrent.Count(r => (int)r.StatusCode == 201);
        var cash = await UseDbAsync(db => DanasAccounts(db).Where(a => a.Name == account).Select(a => a.Cash.Amount).SingleAsync());
        cash.ShouldBeGreaterThanOrEqualTo(Parse(minimum));
        cash.ShouldBe(1381.05m - (400m * succeeded)); // every success, and only successes, moved money
    }

    public void Dispose()
    {
        foreach (var response in _concurrent)
        {
            response.Dispose();
        }
    }

    private async Task SendAsync(object body, string? key)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers") { Content = JsonContent.Create(body) };
        request.Headers.Add(ApiScenario.XsrfHeader, await api.GetAntiforgeryTokenAsync());
        if (key is not null)
        {
            request.Headers.Add(IdempotencyKey, key);
        }

        _last = key is null ? null : (body, key);
        await api.SendAsync(request);
    }

    private async Task<HttpResponseMessage> PostRawAsync(object body, string key, string antiforgeryToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/transfers") { Content = JsonContent.Create(body) };
        request.Headers.Add(ApiScenario.XsrfHeader, antiforgeryToken);
        request.Headers.Add(IdempotencyKey, key);
        return await api.Client.SendAsync(request);
    }

    private async Task<object> BodyAsync(string amount, string from, string toOwnerEmail, string to, uint? version = null)
    {
        var fromAccount = await UseDbAsync(db => DanasAccounts(db).Where(a => a.Name == from).Select(a => new { a.Id, a.Version }).SingleAsync());
        var toId = await UseDbAsync(db => db.Accounts
            .Where(a => db.Clients.Where(c => c.Email == toOwnerEmail).Select(c => c.Id).Contains(a.ClientId) && a.Name == to)
            .Select(a => a.Id).SingleAsync());
        return new { fromAccountId = fromAccount.Id, toAccountId = toId, amount = Parse(amount), fromAccountVersion = version ?? fromAccount.Version };
    }

    private Task<uint> VersionOfAsync(string account) =>
        UseDbAsync(db => DanasAccounts(db).Where(a => a.Name == account).Select(a => a.Version).SingleAsync());

    private static IQueryable<Domain.Account> DanasAccounts(MeridianDbContext db) =>
        db.Accounts.AsNoTracking().Where(a => db.Clients.Where(c => c.Email == DemoPortfolio.DanaEmail).Select(c => c.Id).Contains(a.ClientId));

    private static decimal Parse(string amount) => decimal.Parse(amount, CultureInfo.InvariantCulture);

    private async Task<T> UseDbAsync<T>(Func<MeridianDbContext, Task<T>> query)
    {
        await using var scope = api.Factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<MeridianDbContext>());
    }
}
