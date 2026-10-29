using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Meridian.Infrastructure.DemoData;
using Meridian.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class PositionsSteps(ApiScenario api)
{
    private Guid _accountId;

    [When(@"I request the positions in (Dana|Raj)'s ""(.*)""")]
    public async Task WhenIRequestPositions(string persona, string accountName) =>
        await RequestAsync(persona, accountName, string.Empty);

    [When(@"I request page (\d+) of (Dana|Raj)'s ""(.*)"" positions, (\d+) per page, sorted by ""(.*)"" ""(.*)""")]
    public async Task WhenIRequestAPage(int page, string persona, string accountName, int pageSize, string sort, string order) =>
        await RequestAsync(persona, accountName, $"?page={page}&pageSize={pageSize}&sort={sort}&order={order}");

    [When(@"I request (Dana|Raj)'s ""(.*)"" positions filtered by ""(.*)""")]
    public async Task WhenIRequestFiltered(string persona, string accountName, string filter) =>
        await RequestAsync(persona, accountName, $"?filter={Uri.EscapeDataString(filter)}");

    [When(@"I request (Dana|Raj)'s ""(.*)"" positions sorted by ""(.*)""")]
    public async Task WhenIRequestSorted(string persona, string accountName, string sort) =>
        await RequestAsync(persona, accountName, $"?sort={Uri.EscapeDataString(sort)}");

    [Then(@"the positions are ""(.*)""")]
    public async Task ThenThePositionsAre(string symbols)
    {
        ((int)api.LastResponse.StatusCode).ShouldBe(200, await api.LastResponse.Content.ReadAsStringAsync());
        using var body = await ReadAsync();
        var actual = body.RootElement.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("symbol").GetString()).ToList();
        actual.ShouldBe(symbols.Split(", "));
    }

    [Then(@"there are (\d+) positions on (\d+) pages")]
    public async Task ThenThereAreNPositionsOnPages(int total, int pages)
    {
        using var body = await ReadAsync();
        body.RootElement.GetProperty("totalCount").GetInt32().ShouldBe(total);
        body.RootElement.GetProperty("totalPages").GetInt32().ShouldBe(pages);
    }

    [Then("the positions' market values add up to the account's value less its cash")]
    public async Task ThenMarketValuesAddUp()
    {
        using var positions = await ReadAsync();
        var sum = positions.RootElement.GetProperty("items").EnumerateArray().Sum(p => p.GetProperty("marketValue").GetProperty("amount").GetDecimal());

        await api.GetAsync($"/api/v1/accounts/{_accountId}");
        using var account = await ReadAsync();
        var total = account.RootElement.GetProperty("totalValue").GetProperty("amount").GetDecimal();
        var cash = account.RootElement.GetProperty("cash").GetProperty("amount").GetDecimal();
        sum.ShouldBe(total - cash);
    }

    private async Task RequestAsync(string persona, string accountName, string queryString)
    {
        var email = persona == "Dana" ? DemoPortfolio.DanaEmail : DemoPortfolio.RajEmail;
        await using var scope = api.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
        var client = await db.Clients.SingleAsync(c => c.Email == email);
        _accountId = await db.Accounts.Where(a => a.ClientId == client.Id && a.Name == accountName).Select(a => a.Id).SingleAsync();
        await api.GetAsync($"/api/v1/accounts/{_accountId}/positions{queryString}");
    }

    private async Task<JsonDocument> ReadAsync() =>
        JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
}
