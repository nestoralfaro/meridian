using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class PlannerSteps(ApiScenario api)
{
    private static readonly object RajsPlan = new
    {
        currentSavings = 250_000m,
        monthlyContribution = 1_000m,
        currentAge = 52,
        retirementAge = 65,
        annualSpending = 40_000m,
        stockPercent = 60,
    };

    private readonly List<string> _bodies = [];

    [When(@"I run a plan with (-?\d+) saved, (-?\d+) a month, age (\d+) retiring at (\d+), spending (-?\d+) a year, (\d+)% stocks")]
    public async Task WhenIRunAPlan(decimal saved, decimal monthly, int age, int retireAt, decimal spending, int stocks) =>
        await api.PostAsync("/api/v1/planner/simulations", new
        {
            currentSavings = saved,
            monthlyContribution = monthly,
            currentAge = age,
            retirementAge = retireAt,
            annualSpending = spending,
            stockPercent = stocks,
        });

    [When("I run the same plan twice")]
    public async Task WhenIRunTheSamePlanTwice()
    {
        for (var i = 0; i < 2; i++)
        {
            await api.PostAsync("/api/v1/planner/simulations", RajsPlan);
            _bodies.Add(await api.LastResponse.Content.ReadAsStringAsync());
        }
    }

    [Then("both results are identical")]
    public void ThenBothResultsAreIdentical()
    {
        _bodies.Count.ShouldBe(2);
        _bodies[1].ShouldBe(_bodies[0]);
    }

    [Then(@"the success rate is between 0 and 1")]
    public async Task ThenTheSuccessRateIsAProbability()
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("successRate").GetDecimal().ShouldBeInRange(0m, 1m);
    }

    [Then(@"there is a projection for every age from (\d+) to (\d+)")]
    public async Task ThenThereIsAProjectionForEveryAge(int from, int to)
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("years").EnumerateArray().Select(y => y.GetProperty("age").GetInt32())
            .ShouldBe(Enumerable.Range(from, to - from + 1));
    }

    [Then(@"the problem names the field ""(.*)""")]
    public async Task ThenTheProblemNamesTheField(string field)
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}
