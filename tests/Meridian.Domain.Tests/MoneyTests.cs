namespace Meridian.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void AddsAndSubtractsAmountsInTheSameCurrencyExactly()
    {
        // Binary floating point would give 0.30000000000000004; money must not.
        (Money.Usd(0.10m) + Money.Usd(0.20m)).ShouldBe(Money.Usd(0.30m));
        (Money.Usd(10.00m) - Money.Usd(0.01m)).ShouldBe(Money.Usd(9.99m));
    }

    [Fact]
    public void RefusesToMixCurrencies()
    {
        var exception = Should.Throw<InvalidOperationException>(() => Money.Usd(1m) + new Money(1m, "EUR"));

        exception.Message.ShouldContain("USD");
        exception.Message.ShouldContain("EUR");
    }

    [Theory]
    [InlineData("1.005", "1.00")] // banker's rounding: ties go to the even cent
    [InlineData("1.015", "1.02")]
    [InlineData("2.675", "2.68")]
    [InlineData("-1.005", "-1.00")]
    public void RoundsToCentsWithBankersRounding(string amount, string expected)
    {
        Money.Usd(decimal.Parse(amount, CultureInfo.InvariantCulture)).RoundToCents()
            .ShouldBe(Money.Usd(decimal.Parse(expected, CultureInfo.InvariantCulture)));
    }

    [Fact]
    public void MultipliesByAQuantity()
    {
        (Money.Usd(245.30m) * 40m).ShouldBe(Money.Usd(9812.00m));
        (Money.Usd(64.12m) * 1.5m).ShouldBe(Money.Usd(96.18m));
    }

    [Fact]
    public void SumsASequenceInOneCurrency()
    {
        new[] { Money.Usd(1.10m), Money.Usd(2.20m), Money.Usd(3.30m) }.Sum("USD").ShouldBe(Money.Usd(6.60m));
        Array.Empty<Money>().Sum("USD").ShouldBe(Money.Zero("USD"));
    }

    [Fact]
    public void RequiresAThreeLetterIsoCurrencyCode()
    {
        Should.Throw<ArgumentException>(() => new Money(1m, "usd"));
        Should.Throw<ArgumentException>(() => new Money(1m, "US"));
    }

    [Fact]
    public void ComparesAmountsInTheSameCurrency()
    {
        (Money.Usd(5m) > Money.Usd(4.99m)).ShouldBeTrue();
        (Money.Usd(-0.01m) < Money.Zero("USD")).ShouldBeTrue();
    }
}
