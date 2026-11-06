using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EthangRss.Core.YouTube;

/// <summary>One video as a channel page presents it.</summary>
public sealed record ChannelVideo(
    string VideoId,
    string Title,
    TimeSpan? Duration,
    DateTimeOffset? PublishedAt)
{
    public string Link => "https://www.youtube.com/watch?v=" + VideoId;
}

/// <summary>
/// Reads a channel's /videos page. YouTube no longer serves the Atom feed that used to carry
/// this, so the page's own bootstrap JSON is the source. That shape changes without notice, which
/// is why every lookup here is optional and a page that cannot be read yields no videos rather
/// than throwing: the caller records an empty fetch as a failure it can see.
/// </summary>
public static partial class ChannelPageParser
{
    public static IReadOnlyList<ChannelVideo> ParseVideos(string html, DateTimeOffset? now = null)
    {
        var videos = new List<ChannelVideo>();
        var clock = now ?? DateTimeOffset.UtcNow;

        foreach (var entry in Entries(html))
        {
            var video = ReadEntry(entry, clock);
            if (video is not null)
            {
                videos.Add(video);
            }
        }

        return videos;
    }

    /// <summary>The channel's display name, or null when the page does not carry one.</summary>
    public static string? ParseChannelTitle(string html)
    {
        var match = ChannelTitlePattern().Match(html);
        return match.Success ? Unescape(match.Groups[1].Value) : null;
    }

    /// <summary>The channel's own id, which is what a handle address has to be resolved to.</summary>
    public static string? ParseChannelId(string html)
    {
        var match = ExternalIdPattern().Match(html);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Reads the relative age the page shows ("14h ago", "5d ago"). It is the only date the grid
    /// carries, and it is coarse by design, so it is a starting point the fetcher refines from the
    /// watch page rather than a final answer.
    /// </summary>
    public static DateTimeOffset? ParseRelativeTime(string? text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        if (trimmed.Equals("just now", StringComparison.OrdinalIgnoreCase))
        {
            return now;
        }

        var match = RelativeTimePattern().Match(trimmed);
        if (!match.Success)
        {
            return null;
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return match.Groups[2].Value.ToLowerInvariant() switch
        {
            "s" => now.AddSeconds(-amount),
            "m" => now.AddMinutes(-amount),
            "h" => now.AddHours(-amount),
            "d" => now.AddDays(-amount),
            "w" => now.AddDays(-amount * 7),
            "mo" => now.AddDays(-amount * 30),
            "y" => now.AddDays(-amount * 365),
            _ => null,
        };
    }

    private static ChannelVideo? ReadEntry(JsonElement entry, DateTimeOffset now)
    {
        var id = FirstString(entry, "videoId") ?? IdFromThumbnail(entry);
        if (id is null || id.Length != 11)
        {
            return null;
        }

        var title = FirstString(entry, "content") ?? FirstString(entry, "text") ?? id;
        var duration = ParseDuration(DurationBadge(entry));
        var published = ParseRelativeTime(AgeText(entry), now);

        return new ChannelVideo(id, title, duration, published);
    }

    private static IEnumerable<JsonElement> Entries(string html)
    {
        var data = InitialData(html);
        if (data is null)
        {
            yield break;
        }

        var found = new List<JsonElement>();
        Walk(data.Value, found);
        foreach (var entry in found)
        {
            yield return entry;
        }
    }

    private static void Walk(JsonElement element, List<JsonElement> found)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "lockupViewModel" && property.Value.ValueKind == JsonValueKind.Object)
                    {
                        found.Add(property.Value);
                        continue;
                    }

                    Walk(property.Value, found);
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    Walk(item, found);
                }

                break;
        }
    }

    private static JsonElement? InitialData(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = InitialDataPattern().Match(html);
        if (!match.Success)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(match.Groups[1].Value);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>The first string value stored under a property name, anywhere in the entry.</summary>
    private static string? FirstString(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == name && property.Value.ValueKind == JsonValueKind.String)
                    {
                        return property.Value.GetString();
                    }

                    var nested = FirstString(property.Value, name);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    var nested = FirstString(item, name);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>The video id, taken from a thumbnail address when no id property is present.</summary>
    private static string? IdFromThumbnail(JsonElement entry)
    {
        var url = FirstString(entry, "url");
        if (url is null)
        {
            return null;
        }

        var match = ThumbnailIdPattern().Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>The duration badge, which is the only place a length appears on the grid.</summary>
    private static string? DurationBadge(JsonElement entry)
    {
        foreach (var text in AllStrings(entry, "text"))
        {
            if (DurationPattern().IsMatch(text))
            {
                return text;
            }
        }

        return null;
    }

    private static string? AgeText(JsonElement entry)
    {
        foreach (var text in AllStrings(entry, "content"))
        {
            if (RelativeTimePattern().IsMatch(text.Trim()))
            {
                return text;
            }
        }

        return null;
    }

    private static IEnumerable<string> AllStrings(JsonElement element, string name)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == name && property.Value.ValueKind == JsonValueKind.String)
                    {
                        yield return property.Value.GetString()!;
                    }

                    foreach (var nested in AllStrings(property.Value, name))
                    {
                        yield return nested;
                    }
                }

                break;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var nested in AllStrings(item, name))
                    {
                        yield return nested;
                    }
                }

                break;
        }
    }

    private static TimeSpan? ParseDuration(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || !DurationPattern().IsMatch(text))
        {
            return null;
        }

        var seconds = 0;
        foreach (var part in text.Split(':'))
        {
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            seconds = (seconds * 60) + value;
        }

        return TimeSpan.FromSeconds(seconds);
    }

    private static string Unescape(string value) =>
        value.Replace("\\u0026", "&", StringComparison.Ordinal).Replace("\\\"", "\"", StringComparison.Ordinal);

    [GeneratedRegex(@"var ytInitialData = (\{.*?\});</script>", RegexOptions.Singleline)]
    private static partial Regex InitialDataPattern();

    [GeneratedRegex(@"""channelMetadataRenderer"":\{""title"":""([^""]+)""")]
    private static partial Regex ChannelTitlePattern();

    [GeneratedRegex(@"""externalId"":""(UC[A-Za-z0-9_-]{22})""")]
    private static partial Regex ExternalIdPattern();

    [GeneratedRegex(@"/vi/([A-Za-z0-9_-]{11})/")]
    private static partial Regex ThumbnailIdPattern();

    [GeneratedRegex(@"^\d+(:\d{2})+$")]
    private static partial Regex DurationPattern();

    [GeneratedRegex(@"^(\d+)\s*(s|m|h|d|w|mo|y)\s+ago$", RegexOptions.IgnoreCase)]
    private static partial Regex RelativeTimePattern();
}