using Meridian.Application.Learn;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Meridian.Api.Endpoints;

internal static class LearnEndpoints
{
    public static IEndpointRouteBuilder MapLearnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // Public and the same for everyone: browsers and proxies may cache it briefly.
        var learn = endpoints.MapGroup("/learn/articles").WithTags("Learn").AllowAnonymous()
            .AddEndpointFilter(async (context, next) =>
            {
                var result = await next(context).ConfigureAwait(false);
                context.HttpContext.Response.Headers.CacheControl = "public, max-age=300";
                return result;
            });

        learn.MapGet("/", (IArticleStore articles) =>
                TypedResults.Ok(new ArticleListResponse([.. articles.List().Select(ToResponse)])))
            .WithName("ListArticles")
            .WithSummary("Published Learn center articles, newest first.");

        learn.MapGet("/{slug}", Results<Ok<ArticleResponse>, ProblemHttpResult> (string slug, IArticleStore articles) =>
                articles.Find(slug) is { } article
                    ? TypedResults.Ok(new ArticleResponse(ToResponse(article.Summary), article.Html))
                    : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Article not found."))
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName("GetArticle")
            .WithSummary("One published article, rendered to sanitized HTML. Drafts are not found.");

        return endpoints;
    }

    private static ArticleSummaryResponse ToResponse(ArticleSummary a) => new(a.Slug, a.Title, a.Summary, a.Published, a.ReadingMinutes);
}

internal sealed record ArticleSummaryResponse(string Slug, string Title, string Summary, DateOnly Published, int ReadingMinutes);

internal sealed record ArticleListResponse(IReadOnlyList<ArticleSummaryResponse> Items);

/// <param name="Html">Rendered from Markdown with raw HTML disabled, then sanitized.</param>
internal sealed record ArticleResponse(ArticleSummaryResponse Summary, string Html);
