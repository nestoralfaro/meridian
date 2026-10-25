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

    private static readonly Transaction[] History =
    [
        .. Enumerable.Range(1, 12).Select(day => new Transaction(Guid.NewGuid(), day % 2 == 0 ? DanaBrokerage.Id : DanaRoth.Id,
            new DateOnly(2026, 10, day), TransactionType.Deposit, $"Deposit {day}", Money.Usd(day * 10m))),
        new Transaction(Guid.NewGuid(), RajIra.Id, new DateOnly(2026, 10, 30), TransactionType.Dividend, "Raj's dividend", Money.Usd(410m)),
    ];

    private readonly AccountQueries _queries = new(new InMemoryAccounts([DanaBrokerage, DanaRoth, RajIra], History));

    [Fact]
    public async Task TheOverviewTotalsTheClientsOwnAccountsOnly()
    {
        var overview = await _queries.GetOverviewAsync(Dana, TestContext.Current.CancellationToken);

        overview.Accounts.Select(a => a.Name).ShouldBe(["Individual Brokerage", "Roth IRA"]);
        overview.TotalValue.ShouldBe(Money.Usd(20781.75m));
        overview.DayChange.ShouldBe(Money.Usd(75.50m + 47.50m)); // brokerage +75.50; Roth: fund +44.00, bond +3.50
    }

    [Fact]
    public async Task AllocationGroupsHoldingsAndCashByAssetClassLargestFirst()
    {
        var overview = await _queries.GetOverviewAsync(Dana, TestContext.Current.CancellationToken);

        overview.Allocation.Select(s => (s.AssetClass, s.Value)).ShouldBe([
            (AssetClass.Fund, Money.Usd(14718.00m)),  // MDTOT: 60 × 245.30
            (AssetClass.Bond, Money.Usd(3206.00m)),   // MDBND: 50 × 64.12
            (AssetClass.Cash, Money.Usd(1570.25m)),   // uninvested cash across both accounts
            (AssetClass.Stock, Money.Usd(1287.50m)),  // ACME: 10 × 128.75
        ]);
    }

    [Fact]
    public async Task AllocationPercentagesAlwaysAddUpToExactly100()
    {
        // Rounded independently these would be 70.82 + 15.43 + 7.56 + 6.20 = 100.01%. Largest-remainder rounding fixes it.
        var overview = await _queries.GetOverviewAsync(Dana, TestContext.Current.CancellationToken);

        overview.Allocation.Sum(s => s.Percent).ShouldBe(100.00m);
        overview.Allocation.Select(s => s.Percent).ShouldBe([70.82m, 15.43m, 7.56m, 6.19m]); // the two largest remainders (Bond, Cash) get the missing cents
    }

    [Fact]
    public async Task RecentActivityIsTheClientsNewestTransactionsFirst()
    {
        var activity = await _queries.GetRecentActivityAsync(Dana, limit: 5, TestContext.Current.CancellationToken);

        activity.Select(a => a.Description).ShouldBe(["Deposit 12", "Deposit 11", "Deposit 10", "Deposit 9", "Deposit 8"]);
        activity[0].AccountName.ShouldBe("Individual Brokerage");
        activity.ShouldNotContain(a => a.Description == "Raj's dividend"); // never another client's activity
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(500, 50)]
    public async Task TheActivityLimitIsClamped(int requested, int effective)
    {
        var many = new AccountQueries(new InMemoryAccounts([DanaBrokerage], [.. Enumerable.Range(0, 80).Select(i =>
            new Transaction(Guid.NewGuid(), DanaBrokerage.Id, new DateOnly(2026, 1, 1).AddDays(i), TransactionType.Deposit, $"T{i}", Money.Usd(1m)))]));

        var activity = await many.GetRecentActivityAsync(Dana, requested, TestContext.Current.CancellationToken);

        activity.Count.ShouldBe(effective);
    }

    [Fact]
    public async Task AClientWithNoAccountsGetsAnEmptyOverview()
    {
        var overview = await _queries.GetOverviewAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        overview.Accounts.ShouldBeEmpty();
        overview.Allocation.ShouldBeEmpty();
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

    private sealed class InMemoryAccounts(Account[] accounts, Transaction[]? transactions = null) : IAccountRepository
    {
        public InMemoryAccounts(params Account[] accounts)
            : this(accounts, null)
        {
        }

        public Task<IReadOnlyList<(Transaction Transaction, string AccountName)>> RecentTransactionsAsync(Guid clientId, int limit, CancellationToken cancellationToken = default)
        {
            var owned = accounts.Where(a => a.ClientId == clientId).ToDictionary(a => a.Id, a => a.Name);
            return Task.FromResult<IReadOnlyList<(Transaction, string)>>([.. (transactions ?? [])
                .Where(t => owned.ContainsKey(t.AccountId))
                .OrderByDescending(t => t.Date).ThenByDescending(t => t.Id)
                .Take(limit)
                .Select(t => (t, owned[t.AccountId]))]);
        }

        public Task<IReadOnlyList<Account>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<Account>>([.. accounts.Where(a => a.ClientId == clientId)]);

        public Task<Account?> FindAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(accounts.SingleOrDefault(a => a.Id == accountId));
    }
}
