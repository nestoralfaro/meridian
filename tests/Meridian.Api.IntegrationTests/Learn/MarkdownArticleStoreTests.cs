using Meridian.Infrastructure.Learn;

namespace Meridian.Api.IntegrationTests.Learn;

public sealed class MarkdownArticleStoreTests
{
    private static KeyValuePair<string, string> Source(string slug, string body, string published = "2026-10-29") =>
        new($"{slug}.md", $"---\ntitle: {slug} title\nsummary: About {slug}.\npublished: {published}\n---\n\n{body}");

    [Fact]
    public void TheRepositorysArticlesAllParseAndDraftsStayHidden()
    {
        var store = new MarkdownArticleStore();

        store.List().Select(a => a.Slug).ShouldBe(["monte-carlo-projections", "asset-allocation", "unrealized-gains"], ignoreOrder: true);
        store.Find("fees-draft").ShouldBeNull();
    }

    [Fact]
    public void MarkdownBecomesHtmlWithHeadingAnchors()
    {
        var store = new MarkdownArticleStore([Source("basics", "## The short version\n\nStocks **grow**.")]);

        var article = store.Find("basics")!;
        article.Html.ShouldContain("<h2 id=\"the-short-version\">The short version</h2>");
        article.Html.ShouldContain("<strong>grow</strong>");
        article.Summary.Title.ShouldBe("basics title");
    }

    [Theory]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[click](javascript:alert(1))")]
    public void UnsafeContentNeverReachesThePage(string markdown)
    {
        var html = new MarkdownArticleStore([Source("evil", markdown)]).Find("evil")!.Html;

        // At most harmless escaped text (&lt;img …&gt;): never a real element, handler or script URL.
        html.ShouldNotContain("<script", Case.Insensitive);
        html.ShouldNotContain("<img", Case.Insensitive);
        html.ShouldNotContain("href=\"javascript", Case.Insensitive);
    }

    [Fact]
    public void ReadingTimeIsAboutTwoHundredWordsAMinute()
    {
        var words = string.Join(' ', Enumerable.Repeat("word", 401));

        new MarkdownArticleStore([Source("long", words)]).Find("long")!.Summary.ReadingMinutes.ShouldBe(3);
        new MarkdownArticleStore([Source("short", "Hi.")]).Find("short")!.Summary.ReadingMinutes.ShouldBe(1);
    }

    [Fact]
    public void NewestArticlesComeFirst()
    {
        var store = new MarkdownArticleStore([Source("older", "x", "2026-01-01"), Source("newer", "x", "2026-06-01")]);

        store.List().Select(a => a.Slug).ShouldBe(["newer", "older"]);
    }

    [Fact]
    public void ArticlesWithoutFrontMatterFailLoudly()
    {
        Should.Throw<InvalidOperationException>(() => new MarkdownArticleStore([new("broken.md", "# No front matter")]));
    }
}
