using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EthangRss.Core.Content;

/// <summary>
/// Recovers the article text from a page that only published a teaser in its feed. Feed bodies are
/// frequently truncated to a sentence and a link, so the rest of the article has to be read from the
/// page itself. The page is somebody else's markup: navigation, tag clouds, and footers surround the
/// prose, and the job here is to pick the prose out and hand back markup the block renderer already
/// understands.
/// </summary>
/// <remarks>
/// The rule is density, not a per-site rule: a region is a candidate when it holds enough text, and it
/// wins when most of that text is prose rather than links. A tag cloud is nearly all links and loses;
/// a paragraph is nearly all words and wins. Nothing here is specific to any one site, which is the
/// point - a list of selectors would rot with the first redesign.
/// </remarks>
public static partial class ArticleExtractor
{
    /// <summary>
    /// What may appear between a tag's name and its closing '&gt;': quoted attribute values, or any
    /// character that is neither a quote nor the closing bracket. Real pages carry values such as
    /// class="[&>a]:hidden", where a plain [^&gt;]* would end the tag in the middle of an attribute
    /// and leave the rest of it to be read as article text.
    /// </summary>
    private const string TagBody = "(?:(?:[^>\"']|\"[^\"]*\"|'[^']*')*)";

    /// <summary>The shortest run of text worth calling an article body.</summary>
    private const int MinimumTextLength = 120;

    /// <summary>Above this share of link text, a region is a menu rather than prose.</summary>
    private const double MaximumLinkDensity = 0.35;

    /// <summary>
    /// Returns markup for the article's own text, or null when the page offers no prose to recover.
    /// The markup is HTML the block parser already understands, so structure survives the trip.
    /// </summary>
    public static string? Extract(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var cleaned = Prepare(html);
        return BestRegion(cleaned) ?? Paragraphs(cleaned);
    }

    /// <summary>
    /// Drops the parts of a page that are never article text, so a footer's worth of links is not
    /// weighed against a paragraph. Only elements that cannot contain the article are removed
    /// outright: a <header> or <nav> landmark sometimes wraps the whole page, and deleting it would
    /// delete the article with it, so those are unwrapped instead and left to lose on density.
    /// </summary>
    private static string Prepare(string html)
    {
        var cleaned = Comment().Replace(html, " ");
        cleaned = Boilerplate().Replace(cleaned, " ");

        // A landmark is chrome unless it wraps the page: some sites put the whole document inside
        // a <header> or <nav>, and deleting that would delete the article with it. A landmark is
        // judged by how much of the page's text it holds - a menu holds a sliver, a wrapper holds
        // nearly everything - and only the sliver is removed.
        var pageTextLength = Math.Max(1, TextOf(cleaned).Length);
        return Landmark().Replace(cleaned, match =>
            TextOf(match.Groups[2].Value).Length * 2 >= pageTextLength ? match.Value : " ");
    }

    /// <summary>
    /// Scores every block-level container and returns the inner markup of the best one. Ties go to
    /// the smaller region, so an article inside a wrapper div is chosen over the wrapper: the text is
    /// the same and the wrapper only adds chrome.
    /// </summary>
    private static string? BestRegion(string html)
    {
        string? bestMarkup = null;
        var bestScore = 0.0;
        var bestTextLength = 0;

        foreach (Match open in Container().Matches(html))
        {
            var tag = open.Groups[1].Value;
            var start = open.Index + open.Length;
            var end = MatchingClose(html, tag, start);
            if (end < 0)
            {
                continue;
            }

            var inner = html[start..end];
            var text = TextOf(inner);
            if (text.Length < MinimumTextLength)
            {
                continue;
            }

            var linkDensity = LinkDensity(inner, text.Length);
            if (linkDensity > MaximumLinkDensity)
            {
                continue;
            }

            var score = text.Length * (1 - linkDensity) * ProseBoost(text);
            var isBetter = score > bestScore;
            var isTighter = bestMarkup is not null
                && score >= bestScore * 0.99
                && text.Length >= bestTextLength * 0.99
                && inner.Length < bestMarkup.Length;

            if (isBetter || isTighter)
            {
                bestMarkup = inner;
                bestScore = Math.Max(score, bestScore);
                bestTextLength = Math.Max(text.Length, bestTextLength);
            }
        }

        return bestMarkup;
    }

    /// <summary>
    /// The last resort for a page with no container at all: keep every paragraph that reads like a
    /// sentence. A page whose paragraphs are all one-line teasers still yields those, which is no
    /// worse than the feed body the reader already had.
    /// </summary>
    private static string? Paragraphs(string html)
    {
        var kept = new StringBuilder();
        var textLength = 0;

        foreach (Match paragraph in ParagraphTag().Matches(html))
        {
            var inner = paragraph.Groups[1].Value;
            var text = TextOf(inner);
            if (text.Length < 40)
            {
                continue;
            }

            kept.Append("<p>").Append(inner).Append("</p>");
            textLength += text.Length;
        }

        return textLength >= MinimumTextLength ? kept.ToString() : null;
    }

    /// <summary>
    /// The index of the tag that closes the one opened before <paramref name="start"/>, or -1 when
    /// the page never closes it. Nesting is counted rather than assumed, because a div inside a div
    /// would otherwise end the region at the inner one's close.
    /// </summary>
    private static int MatchingClose(string html, string tag, int start)
    {
        var depth = 1;
        var position = start;
        var scan = new Regex("<(/)?" + tag + "\\b" + TagBody + ">", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        while (position < html.Length)
        {
            var match = scan.Match(html, position);
            if (!match.Success)
            {
                return -1;
            }

            depth += match.Groups[1].Value.Length > 0 ? -1 : 1;
            position = match.Index + match.Length;

            if (depth == 0)
            {
                return match.Index;
            }
        }

        return -1;
    }

    /// <summary>The region's words, with tags, entities, and runs of whitespace normalized away.</summary>
    private static string TextOf(string fragment)
    {
        var text = WebUtility.HtmlDecode(Tag().Replace(fragment, " "));
        return Whitespace().Replace(text, " ").Trim();
    }

    /// <summary>How much of the region's text sits inside links, which is what a menu is made of.</summary>
    private static double LinkDensity(string fragment, int textLength)
    {
        if (textLength == 0)
        {
            return 1;
        }

        var linked = Anchor().Matches(fragment).Sum(match => TextOf(match.Groups[1].Value).Length);
        return Math.Min(1, (double)linked / textLength);
    }

    /// <summary>
    /// Rewards punctuation a sentence has and a menu does not. Without it, a long list of short
    /// linked headings can out-weigh a shorter, denser article.
    /// </summary>
    private static double ProseBoost(string text) =>
        0.5 + (Math.Min(1, (double)SentencePunctuation().Count(text) / text.Length * 20) * 0.5);

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex(
        @"<(script|style|noscript|svg|head|form|button|select|iframe|template)\b" + TagBody + @">.*?</\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Boilerplate();

    /// <summary>Landmarks that are usually chrome but may wrap the article: unwrapped, not deleted.</summary>
    [GeneratedRegex(
        @"<(nav|header|footer|aside)\b" + TagBody + @">(.*?)</\1\s*>",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Landmark();

    [GeneratedRegex(@"<(article|main|section|div|td)\b" + TagBody + ">", RegexOptions.IgnoreCase)]
    private static partial Regex Container();

    [GeneratedRegex(@"<p\b[^>]*>(.*?)</p>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphTag();

    [GeneratedRegex(@"<a\b[^>]*>(.*?)</a>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex Anchor();

    // A '>' inside a quoted attribute value does not end a tag, and real pages carry values such as
    // class="[&>a]:hidden". Matching the quoted run first is what keeps that attribute out of the
    // article text.
    [GeneratedRegex("<(?:[^>\"']|\"[^\"]*\"|'[^']*')*>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"[ \t\r\n]+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"[,.;:!?]")]
    private static partial Regex SentencePunctuation();
}