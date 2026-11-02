using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace EthangRss.Core.Feeds;

/// <summary>
/// Turns an RSS or Atom document into normalized articles. Namespace-tolerant: elements are
/// matched by local name, so RSS 0.9x/2.0 and Atom all take the same path.
/// </summary>
public static class FeedParser
{
    private const string TitlePlaceholder = "No Title";

    public static ParseResult Parse(string xml, Uri feedAddress)
    {
        ArgumentNullException.ThrowIfNull(feedAddress);

        if (string.IsNullOrWhiteSpace(xml))
        {
            return ParseResult.Failure("The feed document was empty.");
        }

        XDocument document;
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            document = XDocument.Load(reader);
        }
        catch (XmlException exception)
        {
            return ParseResult.Failure($"The feed is not valid XML: {exception.Message}");
        }

        try
        {
            var root = document.Root;
            if (root is null)
            {
                return ParseResult.Failure("The feed document has no root element.");
            }

            var isAtom = NameIs(root, "feed");
            var container = isAtom ? root : FindChild(root, "channel");
            if (container is null)
            {
                return ParseResult.Failure("The document is neither an RSS channel nor an Atom feed.");
            }

            var title = ChildText(container, "title") ?? feedAddress.Host;
            var website = isAtom
                ? ResolveAtomFeedWebsite(container, feedAddress)
                : ResolveRssFeedWebsite(container, feedAddress);

            var items = isAtom
                ? ReadEntries(container, feedAddress)
                : ReadItems(container, feedAddress);

            return ParseResult.Success(new ParsedFeed(
                title.Trim(),
                website,
                ResolveIcon(container, website, feedAddress),
                items));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return ParseResult.Failure($"The feed could not be read: {exception.Message}");
        }
    }

    private static List<FeedItem> ReadItems(XElement channel, Uri feedAddress)
    {
        var items = new List<FeedItem>();
        foreach (var element in channel.Elements().Where(element => NameIs(element, "item")))
        {
            var link = ChildText(element, "link") ?? string.Empty;
            var title = ChildText(element, "title") ?? TitlePlaceholder;
            var content = ChildText(element, "description")
                          ?? ChildText(element, "encoded")
                          ?? ChildText(element, "content")
                          ?? string.Empty;
            var guid = ChildText(element, "guid") ?? link;
            var published = DateNormalizer.Parse(
                ChildText(element, "pubDate") ?? ChildText(element, "date") ?? ChildText(element, "published"));

            Add(items, guid, title, link, content, published, feedAddress);
        }

        return items;
    }

    private static List<FeedItem> ReadEntries(XElement feed, Uri feedAddress)
    {
        var items = new List<FeedItem>();
        foreach (var element in feed.Elements().Where(element => NameIs(element, "entry")))
        {
            var link = ResolveAtomEntryLink(element);
            var title = ChildText(element, "title") ?? TitlePlaceholder;
            var content = ChildText(element, "content")
                          ?? ChildText(element, "summary")
                          ?? ReadMediaBody(element)
                          ?? string.Empty;
            var guid = ChildText(element, "id") ?? link;
            var published = DateNormalizer.Parse(
                ChildText(element, "published") ?? ChildText(element, "updated"));

            Add(items, guid, title, link, content, published, feedAddress);
        }

        return items;
    }

    private static void Add(
        List<FeedItem> items,
        string guid,
        string title,
        string link,
        string content,
        DateTimeOffset? published,
        Uri feedAddress)
    {
        var absolute = MakeAbsolute(link, feedAddress);
        if (ShortsFilter.IsShorts(absolute))
        {
            return;
        }

        items.Add(new FeedItem(
            string.IsNullOrWhiteSpace(guid) ? absolute : guid.Trim(),
            string.IsNullOrWhiteSpace(title) ? TitlePlaceholder : title.Trim(),
            absolute,
            content,
            published ?? DateTimeOffset.UtcNow));
    }

    private static string ResolveAtomEntryLink(XElement entry)
    {
        var links = entry.Elements().Where(element => NameIs(element, "link")).ToList();

        var alternate = links.FirstOrDefault(link =>
            string.Equals((string?)link.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase));
        if (alternate is not null)
        {
            return (string?)alternate.Attribute("href") ?? string.Empty;
        }

        var selfOrFirst = links.FirstOrDefault(link =>
            (string?)link.Attribute("rel") is null
            || !string.Equals((string?)link.Attribute("rel"), "self", StringComparison.OrdinalIgnoreCase));
        return (string?)selfOrFirst?.Attribute("href") ?? string.Empty;
    }

    private static string ResolveRssFeedWebsite(XElement channel, Uri feedAddress)
    {
        var link = ChildText(channel, "link");
        return string.IsNullOrWhiteSpace(link) ? feedAddress.GetLeftPart(UriPartial.Authority) : link.Trim();
    }

    private static string ResolveAtomFeedWebsite(XElement feed, Uri feedAddress)
    {
        var links = feed.Elements().Where(element => NameIs(element, "link")).ToList();

        var alternate = links.FirstOrDefault(link =>
            string.Equals((string?)link.Attribute("rel"), "alternate", StringComparison.OrdinalIgnoreCase));
        var chosen = alternate
                     ?? links.FirstOrDefault(link => string.Equals((string?)link.Attribute("rel"), null, StringComparison.OrdinalIgnoreCase))
                     ?? links.FirstOrDefault();

        var href = (string?)chosen?.Attribute("href");
        return string.IsNullOrWhiteSpace(href) ? feedAddress.GetLeftPart(UriPartial.Authority) : href.Trim();
    }

    /// <summary>
    /// Media RSS (YouTube and podcast feeds) nests the readable text inside media:group, which
    /// sits outside the plain Atom entry fields. The description becomes the body and the
    /// thumbnail leads it, so a video entry still renders as something rather than a bare title.
    /// </summary>
    private static string? ReadMediaBody(XElement entry)
    {
        var group = entry.Descendants().FirstOrDefault(element => NameIs(element, "group"));

        var description = group is null
            ? ChildText(entry, "description")
            : ChildText(group, "description") ?? ChildText(entry, "description");

        var thumbnail = (group ?? entry)
            .Descendants()
            .FirstOrDefault(element => NameIs(element, "thumbnail"))?
            .Attribute("url")?.Value;

        if (string.IsNullOrWhiteSpace(thumbnail))
        {
            return string.IsNullOrWhiteSpace(description) ? null : description;
        }

        var image = "<p><img src=\"" + thumbnail + "\" alt=\"\"></p>";
        return string.IsNullOrWhiteSpace(description) ? image : image + "<p>" + description + "</p>";
    }

    private static string? ResolveIcon(XElement container, string website, Uri feedAddress)
    {
        var icon = ChildText(container, "icon");
        if (!string.IsNullOrWhiteSpace(icon))
        {
            return MakeAbsolute(icon.Trim(), feedAddress) is { Length: > 0 } value ? value : null;
        }

        var root = Uri.TryCreate(website, UriKind.Absolute, out var websiteUri) ? websiteUri : feedAddress;
        var origin = root.GetLeftPart(UriPartial.Authority);
        return string.IsNullOrEmpty(origin) ? null : origin + "/favicon.ico";
    }

    private static string MakeAbsolute(string value, Uri baseAddress)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Uri.TryCreate(baseAddress, value.Trim(), out var absolute) ? absolute.ToString() : value.Trim();
    }

    private static bool NameIs(XElement element, string name) =>
        string.Equals(element.Name.LocalName, name, StringComparison.OrdinalIgnoreCase);

    private static XElement? FindChild(XElement parent, string name) =>
        parent.Elements().FirstOrDefault(element => NameIs(element, name));

    private static string? ChildText(XElement parent, string name)
    {
        var child = FindChild(parent, name);
        if (child is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(child.Value) ? null : child.Value;
    }

    internal static string FormatDate(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
