using System.Text.Json;
using Meridian.Domain;
using Meridian.Infrastructure.DemoData;

namespace Meridian.Api.IntegrationTests.DemoData;

/// <summary>The synthetic dataset behind the demo, the e2e tests and (later) the static demo mode.</summary>
public sealed class DemoPortfolioTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 24);

    [Fact]
    public void IsDeterministicForTheSameDate()
    {
        Fingerprint(DemoPortfolio.Generate(AsOf)).ShouldBe(Fingerprint(DemoPortfolio.Generate(AsOf)));
    }

    [Fact]
    public void DanasAccountsMatchTheValuesInTheDashboardStory()
    {
        var data = DemoPortfolio.Generate(AsOf);
        var dana = data.Clients.Single(c => c.Email == DemoPortfolio.DanaEmail);

        var accounts = data.Accounts.Where(a => a.ClientId == dana.Id).ToDictionary(a => a.Type);

        accounts[AccountType.IndividualBrokerage].TotalValue.ShouldBe(Money.Usd(12480.55m));
        accounts[AccountType.RothIra].TotalValue.ShouldBe(Money.Usd(8301.20m));
    }

    [Fact]
    public void BothPersonasHaveAccountsAndHistory()
    {
        var data = DemoPortfolio.Generate(AsOf);

        foreach (var client in data.Clients)
        {
            var accounts = data.Accounts.Where(a => a.ClientId == client.Id).ToList();
            accounts.ShouldNotBeEmpty();
            accounts.ShouldAllBe(a => data.Transactions.Any(t => t.AccountId == a.Id));
        }

        data.Clients.Select(c => c.Email).ShouldBe([DemoPortfolio.DanaEmail, DemoPortfolio.RajEmail], ignoreOrder: true);
    }

    [Fact]
    public void HistoryCoversTheNinetyDaysBeforeTheDateAndNothingAfter()
    {
        var data = DemoPortfolio.Generate(AsOf);

        data.Transactions.ShouldAllBe(t => t.Date <= AsOf && t.Date > AsOf.AddDays(-90));
        data.Transactions.Select(t => t.Id).Distinct().Count().ShouldBe(data.Transactions.Count);
    }

    [Fact]
    public void EverythingIsObviouslyFictional()
    {
        var data = DemoPortfolio.Generate(AsOf);

        data.Clients.ShouldAllBe(c => c.Email.EndsWith(".test", StringComparison.Ordinal));
        data.Securities.Select(s => s.Symbol).ShouldBe(DemoPortfolio.FictionalSymbols, ignoreOrder: true);
        data.Accounts.ShouldAllBe(a => a.MaskedNumber.StartsWith("****", StringComparison.Ordinal));
    }

    private static string Fingerprint(DemoDataset data) => JsonSerializer.Serialize(new
    {
        clients = data.Clients.Select(c => new { c.Id, c.DisplayName, c.Email }),
        accounts = data.Accounts.Select(a => new { a.Id, a.ClientId, a.Name, Cash = a.Cash.Amount, Positions = a.Positions.Select(p => new { p.SecuritySymbol, p.Quantity }) }),
        transactions = data.Transactions.Select(t => new { t.Id, t.AccountId, t.Date, t.Type, Amount = t.Amount.Amount, t.Symbol }),
    });
}
