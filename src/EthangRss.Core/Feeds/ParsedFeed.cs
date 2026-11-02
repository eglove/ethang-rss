namespace EthangRss.Core.Feeds;

/// <summary>A feed's metadata plus the articles it currently advertises.</summary>
public sealed record ParsedFeed(
    string Title,
    string Website,
    string? IconUrl,
    IReadOnlyList<FeedItem> Items);

/// <summary>Outcome of parsing a feed document.</summary>
public sealed record ParseResult
{
    public ParsedFeed? Feed { get; private init; }

    public string? Error { get; private init; }

    public bool IsSuccess => Feed is not null;

    public static ParseResult Success(ParsedFeed feed) => new() { Feed = feed };

    public static ParseResult Failure(string error) => new() { Error = error };
}
