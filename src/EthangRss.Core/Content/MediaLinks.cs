using System.Text.RegularExpressions;

namespace EthangRss.Core.Content;

/// <summary>A video an article points at, with the identifier an embed needs.</summary>
public sealed record VideoReference(string Provider, string Id)
{
    /// <summary>The embeddable player address, or null when this build will not build one.</summary>
    public string? EmbedUrl => Provider == "youtube"
        ? "https://www.youtube.com/embed/" + Id + "?rel=0&modestbranding=1"
        : null;
}

/// <summary>
/// Recognizes the article links that are really videos, so the reader can play them instead of
/// showing a headline with nothing underneath it.
/// </summary>
public static partial class MediaLinks
{
    public static VideoReference? Resolve(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || !Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host is "youtu.be" or "www.youtu.be" && segments.Length == 1)
        {
            return IsValidId(segments[0]) ? new VideoReference("youtube", segments[0]) : null;
        }

        if (host is not ("youtube.com" or "www.youtube.com" or "m.youtube.com" or "music.youtube.com"))
        {
            return null;
        }

        // watch?v=<id>
        foreach (var pair in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && string.Equals(parts[0], "v", StringComparison.OrdinalIgnoreCase))
            {
                var id = Uri.UnescapeDataString(parts[1]);
                return IsValidId(id) ? new VideoReference("youtube", id) : null;
            }
        }

        // embed/<id>, shorts/<id>, live/<id>, v/<id>
        if (segments.Length >= 2 && segments[0] is "embed" or "shorts" or "live" or "v")
        {
            return IsValidId(segments[1]) ? new VideoReference("youtube", segments[1]) : null;
        }

        return null;
    }

    private static bool IsValidId(string? id) =>
        id is { Length: 11 } && ValidId().IsMatch(id);

    [GeneratedRegex(@"^[A-Za-z0-9_-]{11}$")]
    private static partial Regex ValidId();
}
