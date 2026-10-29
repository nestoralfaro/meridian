namespace Meridian.Domain.Planning;

/// <summary>What a client tells the planner. Amounts are in today's dollars; returns are modelled after inflation.</summary>
/// <param name="StockPercent">Share of the portfolio in stocks (0–100); the rest is bonds.</param>
public sealed record PlanInputs(
    decimal CurrentSavings,
    decimal MonthlyContribution,
    int CurrentAge,
    int RetirementAge,
    decimal AnnualSpending,
    int StockPercent,
    int EndAge = PlanInputs.DefaultEndAge)
{
    public const int DefaultEndAge = 95;
}

/// <summary>Long-run real (after-inflation) return assumptions per asset class. Illustrative, not a forecast.</summary>
public sealed record MarketAssumptions(double StockMean, double StockVolatility, double BondMean, double BondVolatility)
{
    public static readonly MarketAssumptions Default = new(StockMean: 0.05, StockVolatility: 0.16, BondMean: 0.015, BondVolatility: 0.06);
}

public sealed record YearProjection(int Age, decimal P10, decimal P50, decimal P90);

/// <param name="SuccessRate">Share of simulated lifetimes in which savings last to <see cref="PlanInputs.EndAge"/>, 0–1.</param>
public sealed record PlanResult(decimal SuccessRate, int Runs, IReadOnlyList<YearProjection> Years);

/// <summary>
/// Monte Carlo retirement projection: yearly normal returns (Box–Muller from a seeded <see cref="Random"/>), contributions
/// until retirement, then constant real spending. Uses <c>double</c>: this is a statistical illustration, not a ledger;
/// results are rounded to whole dollars. The same inputs and seed always give the same result.
/// </summary>
public static class RetirementSimulator
{
    public const int MaxRuns = 5_000;

    public static PlanResult Simulate(PlanInputs inputs, int runs, int seed, MarketAssumptions? market = null)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentOutOfRangeException.ThrowIfLessThan(runs, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(runs, MaxRuns);
        Validate(inputs);
        market ??= MarketAssumptions.Default;

        var stocks = inputs.StockPercent / 100.0;
        var mean = (stocks * market.StockMean) + ((1 - stocks) * market.BondMean);
        var volatility = (stocks * market.StockVolatility) + ((1 - stocks) * market.BondVolatility);
        var years = inputs.EndAge - inputs.CurrentAge;
        var yearlyContribution = (double)inputs.MonthlyContribution * 12;
        var spending = (double)inputs.AnnualSpending;

        // balances[year][run]: year 0 is today.
        var balances = new double[years + 1][];
        for (var year = 0; year <= years; year++)
        {
            balances[year] = new double[runs];
        }

        var random = new Random(seed);
        var successes = 0;
        for (var run = 0; run < runs; run++)
        {
            var balance = (double)inputs.CurrentSavings;
            var depleted = false;
            balances[0][run] = balance;
            for (var year = 1; year <= years; year++)
            {
                var age = inputs.CurrentAge + year;
                var growth = mean + (volatility * StandardNormal(random)); // drawn every year so runs stay aligned across inputs
                if (!depleted)
                {
                    balance *= 1 + growth;
                    if (age <= inputs.RetirementAge)
                    {
                        balance += yearlyContribution;
                    }
                    else
                    {
                        balance -= spending;
                        depleted = balance <= 0; // once savings run out in retirement, they stay out
                    }

                    balance = Math.Max(balance, 0);
                }

                balances[year][run] = balance;
            }

            if (!depleted)
            {
                successes++;
            }
        }

        var projection = new List<YearProjection>(years + 1);
        for (var year = 0; year <= years; year++)
        {
            Array.Sort(balances[year]);
            projection.Add(new YearProjection(
                inputs.CurrentAge + year,
                Dollars(Percentile(balances[year], 0.10)),
                Dollars(Percentile(balances[year], 0.50)),
                Dollars(Percentile(balances[year], 0.90))));
        }

        return new PlanResult(Math.Round((decimal)successes / runs, 4), runs, projection);
    }

    private static void Validate(PlanInputs inputs)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.CurrentSavings);
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.MonthlyContribution);
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.AnnualSpending);
        ArgumentOutOfRangeException.ThrowIfNegative(inputs.StockPercent);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(inputs.StockPercent, 100);
        ArgumentOutOfRangeException.ThrowIfLessThan(inputs.RetirementAge, inputs.CurrentAge);
        ArgumentOutOfRangeException.ThrowIfLessThan(inputs.EndAge, inputs.RetirementAge + 1);
    }

    /// <summary>Box–Muller transform: one standard normal draw from two uniforms.</summary>
    private static double StandardNormal(Random random)
    {
        var u1 = 1.0 - random.NextDouble(); // (0, 1], so the log is finite
        var u2 = random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
    }

    /// <summary>Linear interpolation between closest ranks of a sorted sample.</summary>
    private static double Percentile(double[] sorted, double p)
    {
        var rank = p * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        return sorted[lower] + ((rank - lower) * (sorted[upper] - sorted[lower]));
    }

    private static decimal Dollars(double value) => Math.Round((decimal)value, 0, MidpointRounding.ToEven);
}
