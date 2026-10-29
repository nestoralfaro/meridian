namespace Meridian.Application.Learn;

public sealed record ArticleSummary(string Slug, string Title, string Summary, DateOnly Published, int ReadingMinutes);

/// <param name="Html">Rendered and sanitized; safe to insert into a page.</param>
public sealed record Article(ArticleSummary Summary, string Html);

/// <summary>Published Learn center articles. Drafts are never returned.</summary>
public interface IArticleStore
{
    IReadOnlyList<ArticleSummary> List();

    Article? Find(string slug);
}
