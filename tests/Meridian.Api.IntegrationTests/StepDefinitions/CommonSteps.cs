using Meridian.Api.IntegrationTests.Infrastructure;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class CommonSteps(ApiScenario api)
{
    [Then("the response status is {int}")]
    public void ThenTheResponseStatusIs(int status) => ((int)api.LastResponse.StatusCode).ShouldBe(status);

    [Then("the response is a problem details document")]
    public void ThenTheResponseIsProblemDetails() =>
        HttpAssertions.MediaTypeOf(api.LastResponse).ShouldBe("application/problem+json");
}
