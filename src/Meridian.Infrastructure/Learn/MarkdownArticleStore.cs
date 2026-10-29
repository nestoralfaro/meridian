using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using Ganss.Xss;
using Markdig;
using Meridian.Application.Learn;

namespace Meridian.Infrastructure.Learn;

/// <summary>
/// Articles are Markdown files in <c>content/learn</c>, reviewed through pull requests like code (a Git-based CMS) and
/// embedded at build time. Raw HTML in Markdown is disabled, and the output is sanitized anyway (defence in depth).
/// Front matter: <c>title</c>, <c>summary</c>, <c>published</c> (a date, or <c>false</c> for a draft).
/// </summary>
internal sealed partial class MarkdownArticleStore : IArticleStore
{
    private const int WordsPerMinute = 200;
    private const string ResourcePrefix = "Meridian.Infrastructure.content.learn.";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder().DisableHtml().UseAutoIdentifiers().Build();
    private readonly Dictionary<string, Article> _published;

    public MarkdownArticleStore()
        : this(EmbeddedArticles())
    {
    }

    /// <param name="sources">File name (the slug plus <c>.md</c>) → Markdown with front matter.</param>
    public MarkdownArticleStore(IEnumerable<KeyValuePair<string, string>> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedAttributes.Add("id"); // heading anchors
        _published = sources
            .Select(source => Parse(Path.GetFileNameWithoutExtension(source.Key), source.Value, sanitizer))
            .OfType<Article>()
            .ToDictionary(a => a.Summary.Slug, StringComparer.Ordinal);
    }

    public IReadOnlyList<ArticleSummary> List() =>
        [.. _published.Values.Select(a => a.Summary).OrderByDescending(a => a.Published).ThenBy(a => a.Title, StringComparer.Ordinal)];

    public Article? Find(string slug) => _published.GetValueOrDefault(slug);

    /// <summary>Null for drafts.</summary>
    private static Article? Parse(string slug, string markdown, HtmlSanitizer sanitizer)
    {
        var match = FrontMatter().Match(markdown.ReplaceLineEndings("\n"));
        if (!match.Success)
        {
            throw new InvalidOperationException($"Article '{slug}' has no front matter.");
        }

        var fields = match.Groups["fields"].Value.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(':', 2))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1].Trim(), StringComparer.Ordinal);
        if (!DateOnly.TryParseExact(fields["published"], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var published))
        {
            return null; // "false" (or anything that isn't a date) is a draft
        }

        var body = match.Groups["body"].Value;
        var words = body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        var html = sanitizer.Sanitize(Markdown.ToHtml(body, Pipeline));
        return new Article(
            new ArticleSummary(slug, fields["title"], fields["summary"], published, Math.Max(1, (int)Math.Ceiling(words / (double)WordsPerMinute))),
            html);
    }

    private static IEnumerable<KeyValuePair<string, string>> EmbeddedArticles()
    {
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)))
        {
            using var stream = assembly.GetManifestResourceStream(name)!;
            using var reader = new StreamReader(stream);
            yield return new(name[ResourcePrefix.Length..], reader.ReadToEnd());
        }
    }

    [GeneratedRegex(@"\A---\n(?<fields>.*?)\n---\n(?<body>.*)\z", RegexOptions.Singleline)]
    private static partial Regex FrontMatter();
}
