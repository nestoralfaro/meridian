namespace Meridian.Domain;

/// <summary>
/// Simulated intraday prices for the demo: a small random step per tick, kept within a band around the previous close
/// so prices stay plausible however long the demo runs. All securities here are fictional.
/// </summary>
public static class PriceWalk
{
    /// <summary>Largest move per tick, as a fraction of the price.</summary>
    public const decimal MaxStep = 0.002m;

    /// <summary>Prices stay within this fraction of the previous close.</summary>
    public const decimal Band = 0.05m;

    /// <param name="uniform">A draw in [0, 1), e.g. <see cref="Random.NextDouble"/>.</param>
    public static Money Next(Money current, Money previousClose, double uniform)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(uniform);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(uniform, 1.0);
        var step = ((decimal)uniform * 2m - 1m) * MaxStep;
        var next = current.Amount * (1m + step);
        var low = previousClose.Amount * (1m - Band);
        var high = previousClose.Amount * (1m + Band);
        return new Money(Math.Round(Math.Clamp(next, low, high), 2, MidpointRounding.ToEven), current.Currency);
    }
}
