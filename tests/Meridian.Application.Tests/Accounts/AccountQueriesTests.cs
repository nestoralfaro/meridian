using Meridian.Application.Accounts;
using Meridian.Domain;

namespace Meridian.Application.Tests.Accounts;

public sealed class AccountQueriesTests
{
    private static readonly Guid Dana = Guid.NewGuid();
    private static readonly Guid Raj = Guid.NewGuid();
    private static readonly Security Fund = new("MDTOT", "Meridian Total Market Index Fund", AssetClass.Fund, Money.Usd(245.30m), Money.Usd(243.10m));
    private static readonly Security Acme = new("ACME", "Acme Corporation", AssetClass.Stock, Money.Usd(128.75m), Money.Usd(130.00m));
    private static readonly Security Bond = new("MDBND", "Meridian Aggregate Bond Fund", AssetClass.Bond, Money.Usd(64.12m), Money.Usd(64.05m));

    private static readonly Account DanaBrokerage = new(Guid.NewGuid(), Dana, "Individual Brokerage", AccountType.IndividualBrokerage, "****4821",
        Money.Usd(1381.05m), [new Position(Fund, 40m, Money.Usd(8800m)), new Position(Acme, 10m, Money.Usd(1100m))]);
    private static readonly Account DanaRoth = new(Guid.NewGuid(), Dana, "Roth IRA", AccountType.RothIra, "****7310",
        Money.Usd(189.20m), [new Position(Fund, 20m, Money.Usd(4500m)), new Position(Bond, 50m, Money.Usd(3150m))]);
    private static readonly Account RajIra = new(Guid.NewGuid(), Raj, "Traditional IRA", AccountType.TraditionalIra, "****5508",
        Money.Usd(3120.40m), [new Position(Bond, 2000m, Money.Usd(127000m))]);

    private readonly AccountQueries _queries = new(new InMemoryAccounts(DanaBrokerage, DanaRoth, RajIra));

    [Fact]
    public async Task TheOverviewTotalsTheClientsOwnAccountsOnly()
    {
        var overview = await _queries.GetOverviewAsync(Dana, TestContext.Current.CancellationToken);

        overview.Accounts.Select(a => a.Name).ShouldBe(["Individual Brokerage", "Roth IRA"]);
        overview.TotalValue.ShouldBe(Money.Usd(20781.75m));
        overview.DayChange.ShouldBe(Money.Usd(75.50m + 47.50m)); // brokerage +75.50; Roth: fund +44.00, bond +3.50
    }

    [Fact]
    public async Task AClientWithNoAccountsGetsAnEmptyOverview()
    {
        var overview = await _queries.GetOverviewAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        overview.Accounts.ShouldBeEmpty();
        overview.TotalValue.ShouldBe(Money.Zero("USD"));
        overview.DayChangePercent.ShouldBe(0m);
    }

    [Fact]
    public async Task AClientCanOpenTheirOwnAccount()
    {
        var lookup = await _queries.GetAccountAsync(Dana, DanaBrokerage.Id, TestContext.Current.CancellationToken);

        lookup.Status.ShouldBe(AccountLookupStatus.Found);
        lookup.Account!.Holdings.Select(h => (h.Symbol, h.MarketValue)).ShouldBe([("MDTOT", Money.Usd(9812.00m)), ("ACME", Money.Usd(1287.50m))]);
    }

    [Fact]
    public async Task AnotherClientsAccountIsReportedSeparatelySoItCanBeAudited()
    {
        var lookup = await _queries.GetAccountAsync(Dana, RajIra.Id, TestContext.Current.CancellationToken);

        lookup.Status.ShouldBe(AccountLookupStatus.BelongsToAnotherClient);
        lookup.Account.ShouldBeNull(); // nothing about the account is returned
    }

    [Fact]
    public async Task AnUnknownAccountIsNotFound()
    {
        var lookup = await _queries.GetAccountAsync(Dana, Guid.NewGuid(), TestContext.Current.CancellationToken);

        lookup.Status.ShouldBe(AccountLookupStatus.NotFound);
    }

    private sealed class InMemoryAccounts(params Account[] accounts) : IAccountRepository
    {
        public Task<IReadOnlyList<Account>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Account>>([.. accounts.Where(a => a.ClientId == clientId)]);

        public Task<Account?> FindAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == accountId));
    }
}
