using Meridian.Api.Auth;
using Meridian.Domain.Planning;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Api.Endpoints;

internal static class PlannerEndpoints
{
    public const int Runs = 2_000;

    /// <summary>Fixed by default so the same inputs always show the same answer (no jitter between clicks).</summary>
    public const int DefaultSeed = 20261029;

    public static IEndpointRouteBuilder MapPlannerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/planner/simulations", Simulate)
            .RequireAuthorization()
            .AddEndpointFilter<ValidateAntiforgery>()
            .WithTags("Planner")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithName("SimulateRetirementPlan")
            .WithSummary($"Monte Carlo projection ({Runs:N0} runs) of whether savings last to age {PlanInputs.DefaultEndAge}. Illustrative, not advice.");
        return endpoints;
    }

    private static Results<Ok<SimulationResponse>, ValidationProblem> Simulate(SimulationRequest request)
    {
        var errors = Validate(request);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var inputs = new PlanInputs(request.CurrentSavings, request.MonthlyContribution, request.CurrentAge, request.RetirementAge,
            request.AnnualSpending, request.StockPercent);
        var result = RetirementSimulator.Simulate(inputs, Runs, request.Seed ?? DefaultSeed);
        var market = MarketAssumptions.Default;
        return TypedResults.Ok(new SimulationResponse(
            result.SuccessRate,
            result.Runs,
            inputs.EndAge,
            [.. result.Years.Select(y => new YearProjectionResponse(y.Age, y.P10, y.P50, y.P90))],
            new AssumptionsResponse(market.StockMean, market.StockVolatility, market.BondMean, market.BondVolatility)));
    }

    private static Dictionary<string, string[]> Validate(SimulationRequest r)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        void Check(bool ok, string field, string message)
        {
            if (!ok)
            {
                errors[field] = [message];
            }
        }

        Check(r.CurrentAge is >= 18 and <= 90, "currentAge", "Enter an age from 18 to 90.");
        Check(r.RetirementAge >= r.CurrentAge && r.RetirementAge < PlanInputs.DefaultEndAge, "retirementAge",
            $"Enter a retirement age from your current age to {PlanInputs.DefaultEndAge - 1}.");
        Check(r.CurrentSavings is >= 0 and <= 100_000_000, "currentSavings", "Enter an amount from $0 to $100,000,000.");
        Check(r.MonthlyContribution is >= 0 and <= 1_000_000, "monthlyContribution", "Enter an amount from $0 to $1,000,000.");
        Check(r.AnnualSpending is >= 0 and <= 10_000_000, "annualSpending", "Enter an amount from $0 to $10,000,000.");
        Check(r.StockPercent is >= 0 and <= 100, "stockPercent", "Enter a percentage from 0 to 100.");
        return errors;
    }
}

/// <param name="AnnualSpending">In today's dollars; the model works after inflation.</param>
/// <param name="StockPercent">0–100; the rest is bonds.</param>
/// <param name="Seed">Optional; the same inputs and seed always give the same result.</param>
internal sealed record SimulationRequest(
    decimal CurrentSavings, decimal MonthlyContribution, int CurrentAge, int RetirementAge, decimal AnnualSpending, int StockPercent, int? Seed = null);

/// <param name="Age">Age at the end of the year.</param>
internal sealed record YearProjectionResponse(int Age, decimal P10, decimal P50, decimal P90);

/// <summary>Real (after-inflation) annual return assumptions, as fractions (0.05 = 5%).</summary>
internal sealed record AssumptionsResponse(double StockMean, double StockVolatility, double BondMean, double BondVolatility);

/// <param name="SuccessRate">0–1: the share of simulated lifetimes in which savings last to <paramref name="EndAge"/>.</param>
internal sealed record SimulationResponse(
    decimal SuccessRate, int Runs, int EndAge, IReadOnlyList<YearProjectionResponse> Years, AssumptionsResponse Assumptions);
