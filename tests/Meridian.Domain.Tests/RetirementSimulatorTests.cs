using Meridian.Domain.Planning;

namespace Meridian.Domain.Tests;

public sealed class RetirementSimulatorTests
{
    private static readonly PlanInputs Raj = new(
        CurrentSavings: 250_000m, MonthlyContribution: 1_000m, CurrentAge: 52, RetirementAge: 65, AnnualSpending: 40_000m, StockPercent: 60);

    /// <summary>No randomness: every year returns exactly the mean, so outcomes can be computed by hand.</summary>
    private static readonly MarketAssumptions Flat = new(StockMean: 0, StockVolatility: 0, BondMean: 0, BondVolatility: 0);

    [Fact]
    public void TheSameInputsAndSeedGiveIdenticalResults()
    {
        var first = RetirementSimulator.Simulate(Raj, runs: 500, seed: 7);
        var second = RetirementSimulator.Simulate(Raj, runs: 500, seed: 7);

        second.SuccessRate.ShouldBe(first.SuccessRate);
        second.Years.ShouldBe(first.Years);
    }

    [Fact]
    public void WithoutGrowthSavingsLastExactlyAsLongAsArithmeticSays()
    {
        // 30 retirement years (66..95) × $10,000 = $300,000 needed; nothing earned, nothing contributed.
        PlanInputs Plan(decimal savings) => new(savings, 0m, CurrentAge: 65, RetirementAge: 65, AnnualSpending: 10_000m, StockPercent: 50);

        RetirementSimulator.Simulate(Plan(300_001m), 10, 1, Flat).SuccessRate.ShouldBe(1m);
        RetirementSimulator.Simulate(Plan(300_000m), 10, 1, Flat).SuccessRate.ShouldBe(0m); // runs out in the final year
    }

    [Fact]
    public void ContributionsAccumulateUntilRetirement()
    {
        var plan = new PlanInputs(0m, MonthlyContribution: 500m, CurrentAge: 60, RetirementAge: 65, AnnualSpending: 0m, StockPercent: 0);

        var result = RetirementSimulator.Simulate(plan, 1, 1, Flat);

        result.Years.Single(y => y.Age == 65).P50.ShouldBe(30_000m); // 5 years × 12 × $500
        result.Years.Single(y => y.Age == 66).P50.ShouldBe(30_000m); // retired: no more contributions
    }

    [Theory]
    [InlineData(100_000, 200_000)]
    [InlineData(250_000, 400_000)]
    [InlineData(400_000, 1_000_000)]
    public void MoreSavingsNeverLowersTheSuccessRate(int less, int more)
    {
        var lower = RetirementSimulator.Simulate(Raj with { CurrentSavings = less }, 1_000, seed: 11);
        var higher = RetirementSimulator.Simulate(Raj with { CurrentSavings = more }, 1_000, seed: 11);

        higher.SuccessRate.ShouldBeGreaterThanOrEqualTo(lower.SuccessRate);
    }

    [Fact]
    public void SpendingMoreNeverRaisesTheSuccessRate()
    {
        var frugal = RetirementSimulator.Simulate(Raj with { AnnualSpending = 30_000m }, 1_000, seed: 3);
        var lavish = RetirementSimulator.Simulate(Raj with { AnnualSpending = 60_000m }, 1_000, seed: 3);

        lavish.SuccessRate.ShouldBeLessThanOrEqualTo(frugal.SuccessRate);
    }

    [Fact]
    public void PercentilesAreOrderedEveryYearAndStartFromTodaysSavings()
    {
        var result = RetirementSimulator.Simulate(Raj, 2_000, seed: 5);

        result.Years.Count.ShouldBe(95 - 52 + 1);
        result.Years[0].ShouldBe(new YearProjection(52, 250_000m, 250_000m, 250_000m));
        result.Years.ShouldAllBe(y => y.P10 <= y.P50 && y.P50 <= y.P90);
        result.SuccessRate.ShouldBeInRange(0m, 1m);
    }

    [Fact]
    public void AgreesWithAnIndependentSimulation()
    {
        // An independent Python implementation of the same model (different RNG, 2,000 runs) gives 20.7% for this plan.
        // The standard error at p ≈ 0.2 and n = 2,000 is ~0.009, so ±0.04 is about four standard errors.
        var result = RetirementSimulator.Simulate(Raj, 2_000, seed: 5);

        result.SuccessRate.ShouldBeInRange(0.167m, 0.247m);
        var atRetirement = result.Years.Single(y => y.Age == 65);
        atRetirement.P90.ShouldBeGreaterThan(atRetirement.P10 * 1.5m); // volatility actually spreads outcomes
    }

    [Theory]
    [InlineData(0)]
    [InlineData(RetirementSimulator.MaxRuns + 1)]
    public void RunsAreBounded(int runs)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RetirementSimulator.Simulate(Raj, runs, seed: 1));
    }

    [Fact]
    public void RejectsImpossibleInputs()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => RetirementSimulator.Simulate(Raj with { MonthlyContribution = -1m }, 10, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => RetirementSimulator.Simulate(Raj with { RetirementAge = 50 }, 10, 1));
        Should.Throw<ArgumentOutOfRangeException>(() => RetirementSimulator.Simulate(Raj with { StockPercent = 101 }, 10, 1));
    }
}
