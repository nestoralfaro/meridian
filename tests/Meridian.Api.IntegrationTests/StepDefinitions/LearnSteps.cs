using System.Text.Json;
using Meridian.Api.IntegrationTests.Infrastructure;
using Reqnroll;

namespace Meridian.Api.IntegrationTests.StepDefinitions;

[Binding]
public sealed class LearnSteps(ApiScenario api)
{
    [Then(@"the articles include ""(.*)"" with a summary and reading time")]
    public async Task ThenTheArticlesInclude(string title)
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        var article = body.RootElement.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("title").GetString() == title);
        article.GetProperty("summary").GetString().ShouldNotBeNullOrWhiteSpace();
        article.GetProperty("readingMinutes").GetInt32().ShouldBeGreaterThan(0);
    }

    [Then("the response may be cached publicly")]
    public void ThenTheResponseMayBeCachedPublicly()
    {
        var cacheControl = HttpAssertions.CacheControlOf(api.LastResponse);
        cacheControl.Public.ShouldBeTrue();
        cacheControl.MaxAge.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Then("the article's HTML starts with a level-2 heading")]
    public async Task ThenTheHtmlStartsWithAHeading()
    {
        using var body = JsonDocument.Parse(await api.LastResponse.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("html").GetString()!.TrimStart().ShouldStartWith("<h2");
    }
}
