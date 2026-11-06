namespace EthangRss.Core.Feeds;

/// <summary>The result of asking a feed for its current contents.</summary>
public sealed record FetchOutcome
{
    public ParsedFeed? Feed { get; private init; }

    public string? Error { get; private init; }

    public int? StatusCode { get; private init; }

    /// <summary>
    /// The address the feed should be stored under, when it differs from the one asked for. A
    /// channel pasted as a handle only reveals its id once its page has been read.
    /// </summary>
    public string? CanonicalAddress { get; private init; }

    public bool IsSuccess => Feed is not null;

    public static FetchOutcome Success(ParsedFeed feed, string? canonicalAddress = null) =>
        new() { Feed = feed, CanonicalAddress = canonicalAddress };

    public static FetchOutcome Failure(string error, int? statusCode = null) =>
        new() { Error = error, StatusCode = statusCode };
}
