namespace EthangRss.Core.Feeds;

/// <summary>One article as it arrived from a feed, before it is stored.</summary>
public sealed record FeedItem(
    string Guid,
    string Title,
    string Link,
    string Content,
    DateTimeOffset? PublishedAt);
