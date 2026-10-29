namespace Meridian.Domain.Tests;

public sealed class PortfolioTests
{
    private static readonly Security TotalMarketFund = new("MDTOT", "Meridian Total Market Index Fund", AssetClass.Fund, Money.Usd(245.30m), Money.Usd(243.10m));
    private static readonly Security Acme = new("ACME", "Acme Corporation", AssetClass.Stock, Money.Usd(128.75m), Money.Usd(130.00m));

    [Fact]
    public void APositionIsWorthItsQuantityAtTheCurrentPrice()
    {
        var position = new Position(TotalMarketFund, quantity: 40m, costBasis: Money.Usd(8800m));

        position.MarketValue.ShouldBe(Money.Usd(9812.00m));
        position.DayChange.ShouldBe(Money.Usd(88.00m)); // (245.30 - 243.10) × 40
    }

    [Fact]
    public void UnrealizedGainIsMarketValueMinusCostBasis()
    {
        var gain = new Position(TotalMarketFund, quantity: 40m, costBasis: Money.Usd(8800m));
        var loss = new Position(Acme, quantity: 10m, costBasis: Money.Usd(1400m));

        gain.UnrealizedGain.ShouldBe(Money.Usd(1012.00m)); // 9812.00 - 8800.00
        gain.UnrealizedGainPercent.ShouldBe(11.50m);       // 1012 / 8800 = 0.115
        loss.UnrealizedGain.ShouldBe(Money.Usd(-112.50m));  // 1287.50 - 1400.00
        loss.UnrealizedGainPercent.ShouldBe(-8.04m);       // -112.50 / 1400 = -0.080357…
    }

    [Fact]
    public void UnrealizedGainPercentIsUndefinedWithoutACostBasis()
    {
        var gifted = new Position(Acme, quantity: 1m, costBasis: Money.Usd(0m));

        gifted.UnrealizedGainPercent.ShouldBeNull();
    }

    [Fact]
    public void AnAccountIsWorthItsCashPlusItsPositions()
    {
        var account = DanasBrokerage();

        account.TotalValue.ShouldBe(Money.Usd(12480.55m));
    }

    [Fact]
    public void AnAccountsDayChangeIsTheSumOfItsPositionsDayChanges()
    {
        var account = DanasBrokerage();

        account.DayChange.ShouldBe(Money.Usd(88.00m - 12.50m)); // ACME fell 1.25 × 10
        account.DayChangePercent.ShouldBe(0.61m); // 75.50 / (12480.55 - 75.50), as a percentage to 2 dp
    }

    [Fact]
    public void QuantitiesMustBePositive()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new Position(Acme, quantity: 0m, costBasis: Money.Usd(0m)));
    }

    private static Account DanasBrokerage() =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Individual Brokerage",
            AccountType.IndividualBrokerage,
            "****4821",
            cash: Money.Usd(1381.05m),
            positions: [new Position(TotalMarketFund, 40m, Money.Usd(8800m)), new Position(Acme, 10m, Money.Usd(1100m))]);
}
