using Meridian.Application.Accounts;
using Meridian.Domain;

namespace Meridian.Application.Tests.Accounts;

public sealed class PositionQueriesTests
{
    private static readonly Guid Raj = Guid.NewGuid();
    private static readonly Guid Dana = Guid.NewGuid();

    // Market values: MDTOT 73,590.00 · MDINT 24,580.00 · ACME 7,725.00 · GLBX 6,327.00
    private static readonly Account RajBrokerage = new(Guid.NewGuid(), Raj, "Individual Brokerage", AccountType.IndividualBrokerage, "****2296",
        Money.Usd(15240.00m),
        [
            new Position(new("MDTOT", "Meridian Total Market Index Fund", AssetClass.Fund, Money.Usd(245.30m), Money.Usd(243.10m)), 300m, Money.Usd(61200m)),
            new Position(new("MDINT", "Meridian International Index Fund", AssetClass.Fund, Money.Usd(61.45m), Money.Usd(61.90m)), 400m, Money.Usd(22800m)),
            new Position(new("GLBX", "Globex Industries", AssetClass.Stock, Money.Usd(42.18m), Money.Usd(41.60m)), 150m, Money.Usd(5400m)),
            new Position(new("ACME", "Acme Corporation", AssetClass.Stock, Money.Usd(128.75m), Money.Usd(130.00m)), 60m, Money.Usd(6900m)),
        ]);

    private readonly AccountQueries _queries = new(new SingleAccount(RajBrokerage));

    private Task<PositionsLookup> Query(PositionsQuery query, Guid? client = null) =>
        _queries.GetPositionsAsync(client ?? Raj, RajBrokerage.Id, query, TestContext.Current.CancellationToken);

    [Fact]
    public async Task ByDefaultTheLargestPositionsComeFirst()
    {
        var lookup = await Query(new PositionsQuery());

        lookup.Status.ShouldBe(AccountLookupStatus.Found);
        lookup.Positions!.Items.Select(p => p.Symbol).ShouldBe(["MDTOT", "MDINT", "ACME", "GLBX"]);
    }

    [Fact]
    public async Task PagesSplitTheSortedListWithoutOverlap()
    {
        var first = (await Query(new PositionsQuery(Page: 1, PageSize: 3))).Positions!;
        var second = (await Query(new PositionsQuery(Page: 2, PageSize: 3))).Positions!;

        first.Items.Select(p => p.Symbol).ShouldBe(["MDTOT", "MDINT", "ACME"]);
        second.Items.Select(p => p.Symbol).ShouldBe(["GLBX"]);
        (first.TotalCount, first.TotalPages).ShouldBe((4, 2));
    }

    [Fact]
    public async Task APageBeyondTheEndIsEmptyButKeepsTheTotals()
    {
        var page = (await Query(new PositionsQuery(Page: 9, PageSize: 3))).Positions!;

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(4);
    }

    [Theory]
    [InlineData(PositionSortField.Symbol, SortOrder.Ascending, new[] { "ACME", "GLBX", "MDINT", "MDTOT" })]
    [InlineData(PositionSortField.UnrealizedGain, SortOrder.Descending, new[] { "MDTOT", "MDINT", "GLBX", "ACME" })] // 12,390 · 1,780 · 927 · 825
    [InlineData(PositionSortField.DayChange, SortOrder.Ascending, new[] { "MDINT", "ACME", "GLBX", "MDTOT" })]       // -180 · -75 · 87 · 660
    [InlineData(PositionSortField.Price, SortOrder.Ascending, new[] { "GLBX", "MDINT", "ACME", "MDTOT" })]
    public async Task SortsByTheRequestedColumn(PositionSortField field, SortOrder order, string[] expected)
    {
        var lookup = await Query(new PositionsQuery(SortBy: field, Order: order));

        lookup.Positions!.Items.Select(p => p.Symbol).ShouldBe(expected);
    }

    [Theory]
    [InlineData("index", new[] { "MDTOT", "MDINT" })] // name, any case
    [InlineData("glb", new[] { "GLBX" })]             // symbol, any case
    [InlineData("  acme ", new[] { "ACME" })]         // surrounding whitespace ignored
    [InlineData("nothing", new string[0])]
    public async Task FiltersBySymbolOrName(string filter, string[] expected)
    {
        var page = (await Query(new PositionsQuery(Filter: filter))).Positions!;

        page.Items.Select(p => p.Symbol).ShouldBe(expected);
        page.TotalCount.ShouldBe(expected.Length); // totals describe the filtered list
    }

    [Fact]
    public async Task EachPositionCarriesItsGainAgainstCostBasis()
    {
        var mdtot = (await Query(new PositionsQuery(Filter: "MDTOT"))).Positions!.Items.Single();

        mdtot.MarketValue.ShouldBe(Money.Usd(73590.00m));
        mdtot.CostBasis.ShouldBe(Money.Usd(61200m));
        mdtot.UnrealizedGain.ShouldBe(Money.Usd(12390.00m));
        mdtot.UnrealizedGainPercent.ShouldBe(20.25m); // 12390 / 61200 = 0.202450…
    }

    [Fact]
    public async Task AnotherClientsAccountIsReportedSeparatelyAndRevealsNothing()
    {
        var lookup = await Query(new PositionsQuery(), client: Dana);

        lookup.Status.ShouldBe(AccountLookupStatus.BelongsToAnotherClient);
        lookup.Positions.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(1, PositionsQuery.MaxPageSize + 1)]
    public async Task RejectsOutOfRangePaging(int page, int pageSize)
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => Query(new PositionsQuery(page, pageSize)));
    }

    private sealed class SingleAccount(Account account) : IAccountRepository
    {
        public Task<Account?> FindAsync(Guid accountId, CancellationToken cancellationToken = default) =>
            Task.FromResult(accountId == account.Id ? account : null);

        public Task<IReadOnlyList<Account>> ListForClientAsync(Guid clientId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<(Transaction Transaction, string AccountName)>> RecentTransactionsAsync(Guid clientId, int limit, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
