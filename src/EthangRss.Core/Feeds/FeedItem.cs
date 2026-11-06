namespace EthangRss.Core.Feeds;

/// <summary>One article as it arrived from a feed, before it is stored.</summary>
/// <param name="Duration">
/// How long the item runs, when the source says. Only YouTube supplies it, and it is what tells a
/// clip from a video: the link alone does not, because a Short is served on a watch address too.
/// </param>
public sealed record FeedItem(
    string Guid,
    string Title,
    string Link,
    string Content,
    DateTimeOffset? PublishedAt,
    TimeSpan? Duration = null);
