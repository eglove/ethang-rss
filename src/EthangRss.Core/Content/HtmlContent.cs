using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace EthangRss.Core.Content;

/// <summary>
/// Converts an article body into structured blocks so a desktop toolkit can draw it natively
/// instead of embedding a browser. Tolerant by design: feed bodies are frequently not well-formed,
/// so anything unrecognized degrades to text rather than failing.
/// </summary>
public static partial class HtmlContent
{
    private const int MaxBlocks = 4000;

    public static ArticleBody Parse(string? html, string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return ArticleBody.Empty;
        }

        var absoluteBase = Uri.TryCreate(baseUrl, UriKind.Absolute, out var parsedBase)
            ? parsedBase
            : null;

        var cleaned = RemoveComment().Replace(html, string.Empty);
        cleaned = RemoveScriptAndStyle().Replace(cleaned, string.Empty);

        var builder = new List<ContentBlock>();
        var position = 0;

        while (position < cleaned.Length && builder.Count < MaxBlocks)
        {
            var match = BlockTag().Match(cleaned, position);
            if (!match.Success)
            {
                AddText(builder, cleaned[position..], absoluteBase);
                break;
            }

            if (match.Index > position)
            {
                AddText(builder, cleaned[position..match.Index], absoluteBase);
            }

            var tag = match.Groups[1].Value.ToLowerInvariant();
            var tagEnd = cleaned.IndexOf('>', match.Index);
            if (tagEnd < 0)
            {
                break;
            }

            // Void elements never have content and must not be searched for a closing tag.
            if (tag is "img" or "hr" or "br")
            {
                AddBlock(builder, tag, cleaned[match.Index..(tagEnd + 1)], absoluteBase);
                position = tagEnd + 1;
                continue;
            }

            var end = FindClosingTag(cleaned, tag, tagEnd + 1);
            var inner = end is null
                ? cleaned[(match.Index + match.Length)..]
                : cleaned[(match.Index + match.Length)..end.Value];

            AddBlock(builder, tag, inner, absoluteBase);

            position = end is null ? cleaned.Length : end.Value + (tag.Length + 3);
        }

        return new ArticleBody(builder);
    }

    private static void AddBlock(
        List<ContentBlock> builder,
        string tag,
        string inner,
        Uri? baseUri)
    {
        switch (tag)
        {
            case "h1":
            case "h2":
            case "h3":
            case "h4":
            case "h5":
            case "h6":
            {
                var text = Flatten(inner);
                if (text.Length > 0)
                {
                    builder.Add(new HeadingBlock(tag[1] - '0', text));
                }

                break;
            }

            case "p":
            case "div":
            case "section":
            case "article":
            {
                AddText(builder, inner, baseUri);
                break;
            }

            case "ul":
            case "ol":
            {
                var items = ListItem().Matches(inner)
                    .Select(match => Flatten(match.Groups[1].Value))
                    .Where(item => item.Length > 0)
                    .ToList();

                if (items.Count > 0)
                {
                    builder.Add(new ListBlock(tag == "ol", items));
                }

                break;
            }

            case "blockquote":
            {
                var children = Parse(inner, baseUri?.ToString() ?? string.Empty).Blocks;
                if (children.Count > 0)
                {
                    builder.Add(new QuoteBlock(children));
                }

                break;
            }

            case "pre":
            {
                var text = WebUtility.HtmlDecode(TagStripper().Replace(inner, string.Empty)).TrimEnd();
                if (text.Trim().Length > 0)
                {
                    builder.Add(new CodeBlock(text));
                }

                break;
            }

            case "img":
            {
                // inner is the tag itself when it is a void element.
                AddImageFromTag(builder, inner, baseUri);
                break;
            }

            case "hr":
            {
                builder.Add(new SeparatorBlock());
                break;
            }

            case "figure":
            {
                foreach (Match image in ImageTag().Matches(inner))
                {
                    AddImageFromTag(builder, image.Value, baseUri);
                }

                break;
            }

            default:
            {
                AddText(builder, inner, baseUri);
                break;
            }
        }
    }

    private static void AddText(List<ContentBlock> builder, string fragment, Uri? baseUri)
    {
        // Loose content outside any block tag: keep it as a paragraph if it has words in it.
        // The fragment may carry an image among its text, so images become their own blocks
        // and the surrounding words become paragraphs.
        var position = 0;
        foreach (Match image in ImageTag().Matches(fragment))
        {
            if (image.Index > position)
            {
                var before = ParseRuns(fragment[position..image.Index], baseUri);
                if (before.Count > 0)
                {
                    builder.Add(new ParagraphBlock(before));
                }
            }

            AddImageFromTag(builder, image.Value, baseUri);
            position = image.Index + image.Length;
        }

        if (position < fragment.Length)
        {
            var runs = ParseRuns(fragment[position..], baseUri);
            if (runs.Count > 0)
            {
                builder.Add(new ParagraphBlock(runs));
            }
        }
    }

    private static void AddImageFromTag(List<ContentBlock> builder, string tagText, Uri? baseUri)
    {
        var source = AttributeValue("src", tagText);
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        var url = MakeAbsolute(source, baseUri);
        if (url is null)
        {
            return;
        }

        var alt = AttributeValue("alt", tagText) ?? string.Empty;
        builder.Add(new ImageBlock(url, WebUtility.HtmlDecode(alt)));
    }

    private static List<InlineRun> ParseRuns(string fragment, Uri? baseUri)
    {
        var runs = new List<InlineRun>();
        var bold = 0;
        var italic = 0;
        var code = 0;
        var position = 0;

        foreach (Match match in InlineTag().Matches(fragment))
        {
            if (match.Index > position)
            {
                Append(runs, fragment[position..match.Index], bold > 0, italic > 0, code > 0, null, baseUri);
            }

            var isClosing = match.Groups[1].Value.Length > 0;
            var tag = match.Groups[2].Value.ToLowerInvariant();

            switch (tag)
            {
                case "strong" or "b":
                    bold += isClosing ? -1 : 1;
                    bold = Math.Max(0, bold);
                    break;
                case "em" or "i":
                    italic += isClosing ? -1 : 1;
                    italic = Math.Max(0, italic);
                    break;
                case "code":
                    code += isClosing ? -1 : 1;
                    code = Math.Max(0, code);
                    break;
                case "a":
                    if (!isClosing)
                    {
                        var hrefIndex = fragment.IndexOf("</a>", match.Index, StringComparison.OrdinalIgnoreCase);
                        var endOfOpen = fragment.IndexOf('>', match.Index);
                        if (endOfOpen >= 0 && hrefIndex > endOfOpen)
                        {
                            var href = AttributeValue("href", fragment[match.Index..(endOfOpen + 1)]);
                            var text = Flatten(fragment[(endOfOpen + 1)..hrefIndex]);
                            if (text.Length > 0)
                            {
                                runs.Add(new InlineRun(text, bold > 0, italic > 0, code > 0, MakeAbsolute(href, baseUri)));
                            }

                            position = hrefIndex + 4;
                            continue;
                        }
                    }

                    break;
            }

            position = match.Index + match.Length;
        }

        if (position < fragment.Length)
        {
            Append(runs, fragment[position..], bold > 0, italic > 0, code > 0, null, baseUri);
        }

        return runs;
    }

    private static void Append(
        List<InlineRun> runs,
        string raw,
        bool isBold,
        bool isItalic,
        bool isCode,
        string? href,
        Uri? baseUri)
    {
        var text = raw.Contains('<', StringComparison.Ordinal)
            ? WebUtility.HtmlDecode(TagStripper().Replace(raw, string.Empty))
            : WebUtility.HtmlDecode(raw);

        text = Whitespace().Replace(text, " ");
        if (text.Length == 0)
        {
            return;
        }

        runs.Add(new InlineRun(text, isBold, isItalic, isCode, href));
    }

    private static string Flatten(string fragment)
    {
        var text = WebUtility.HtmlDecode(TagStripper().Replace(fragment, " "));
        return Whitespace().Replace(text, " ").Trim();
    }

    private static string? AttributeValue(string name, string tagText)
    {
        var match = TagAttribute(name).Match(tagText);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string? MakeAbsolute(string? value, Uri? baseUri)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(value.Trim());
        if (Uri.TryCreate(decoded, UriKind.Absolute, out var absolute))
        {
            return absolute.ToString();
        }

        return baseUri is not null && Uri.TryCreate(baseUri, decoded, out var resolved)
            ? resolved.ToString()
            : decoded;
    }

    private static int? FindClosingTag(string html, string tag, int start)
    {
        var closing = $"</{tag}>";
        var index = html.IndexOf(closing, start, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : index;
    }

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex RemoveComment();

    [GeneratedRegex(@"<(script|style)\b[^>]*>.*?</\1\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex RemoveScriptAndStyle();

    [GeneratedRegex(@"^\s*<(h[1-6]|p|div|section|article|ul|ol|blockquote|pre|img|hr|figure)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockTag();

    [GeneratedRegex(@"<li\b[^>]*>(.*?)</li\s*>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"<img\b[^>]*?\/?>", RegexOptions.IgnoreCase)]
    private static partial Regex ImageTag();

    [GeneratedRegex(@"\ssrc\s*=\s*[`""']?[^`""'>\s]*[`""']?", RegexOptions.IgnoreCase)]
    private static partial Regex SrcCapture();

    [GeneratedRegex(@"<(/?)([a-zA-Z0-9]+)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex InlineTag();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagStripper();

    [GeneratedRegex(@"[ \t\r\n]+")]
    private static partial Regex Whitespace();

    private static Regex TagAttribute(string name) =>
        new($"\\s{name}\\s*=\\s*['\"]?([^'\"\\s>]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
