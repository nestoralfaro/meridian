namespace Meridian.Domain.Tests;

public sealed class PriceWalkTests
{
    private static readonly Money Close = Money.Usd(100m);

    [Theory]
    [InlineData(0.0, 99.80)]   // largest fall
    [InlineData(0.5, 100.00)]  // no change
    [InlineData(0.75, 100.10)]
    public void StepsAreSmallAndInWholeCents(double uniform, decimal expected)
    {
        PriceWalk.Next(Money.Usd(100m), Close, uniform).ShouldBe(Money.Usd(expected));
    }

    [Fact]
    public void PricesStayWithinTheBandAroundThePreviousClose()
    {
        var random = new Random(42);
        var price = Close;
        for (var tick = 0; tick < 100_000; tick++)
        {
            price = PriceWalk.Next(price, Close, random.NextDouble());
            price.Amount.ShouldBeInRange(95m, 105m);
        }
    }

    [Fact]
    public void RejectsDrawsOutsideTheUnitInterval()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => PriceWalk.Next(Close, Close, 1.0));
        Should.Throw<ArgumentOutOfRangeException>(() => PriceWalk.Next(Close, Close, -0.1));
    }
}
