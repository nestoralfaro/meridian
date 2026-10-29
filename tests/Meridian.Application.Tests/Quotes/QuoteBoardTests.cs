using Meridian.Application.Quotes;
using Meridian.Domain;

namespace Meridian.Application.Tests.Quotes;

public sealed class QuoteBoardTests
{
    private static readonly Security Fund = new("MDTOT", "Meridian Total Market Index Fund", AssetClass.Fund, Money.Usd(245.30m), Money.Usd(243.10m));
    private static readonly Security Acme = new("ACME", "Acme Corporation", AssetClass.Stock, Money.Usd(128.75m), Money.Usd(130.00m));

    [Fact]
    public void BeforeAnyTickHoldingsAreValuedAsStored()
    {
        var board = new QuoteBoard();
        board.Load([Fund, Acme]);

        var live = board.Value([new("MDTOT", 40m, Money.Usd(8800m)), new("ACME", 10m, Money.Usd(1100m))]);

        // The same figures the dashboard shows: 40 × 245.30 and (245.30 − 243.10) × 40.
        live.ShouldBe([
            new LivePosition("MDTOT", Money.Usd(245.30m), Money.Usd(9812.00m), Money.Usd(88.00m), Money.Usd(1012.00m), 11.50m),
            new LivePosition("ACME", Money.Usd(128.75m), Money.Usd(1287.50m), Money.Usd(-12.50m), Money.Usd(187.50m), 17.05m),
        ]);
    }

    [Fact]
    public void TicksMovePricesAndValuesFollowWithTheDomainsRounding()
    {
        var board = new QuoteBoard();
        board.Load([Fund]);

        board.Tick(new Random(1));
        var quote = board.Quote("MDTOT")!;
        var live = board.Value([new("MDTOT", 3m, Money.Usd(700m))]).Single();

        quote.Price.ShouldNotBe(Fund.Price);
        live.MarketValue.ShouldBe((quote.Price * 3m).RoundToCents());
        live.DayChange.ShouldBe(((quote.Price - Fund.PreviousClose) * 3m).RoundToCents());
        live.UnrealizedGain.ShouldBe(live.MarketValue - Money.Usd(700m));
    }

    [Fact]
    public void AnAccountIsWorthItsCashPlusLiveHoldings()
    {
        var board = new QuoteBoard();
        board.Load([Fund, Acme]);

        var account = board.ValueAccount(Money.Usd(1381.05m), [new("MDTOT", 40m, Money.Usd(8800m)), new("ACME", 10m, Money.Usd(1100m))]);

        account.TotalValue.ShouldBe(Money.Usd(12480.55m)); // Dana's brokerage, as on the dashboard
        account.DayChange.ShouldBe(Money.Usd(75.50m));
    }

    [Fact]
    public void UnknownSymbolsAreSkipped()
    {
        var board = new QuoteBoard();
        board.Load([Fund]);

        board.Value([new("NOPE", 1m, Money.Usd(1m))]).ShouldBeEmpty();
    }
}
