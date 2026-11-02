namespace EthangRss.Core.Feeds;

/// <summary>The result of asking a feed for its current contents.</summary>
public sealed record FetchOutcome
{
    public ParsedFeed? Feed { get; private init; }

    public string? Error { get; private init; }

    public int? StatusCode { get; private init; }

    public bool IsSuccess => Feed is not null;

    public static FetchOutcome Success(ParsedFeed feed) => new() { Feed = feed };

    public static FetchOutcome Failure(string error, int? statusCode = null) =>
        new() { Error = error, StatusCode = statusCode };
}
