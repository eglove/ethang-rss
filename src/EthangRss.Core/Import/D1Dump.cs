namespace EthangRss.Core.Import;

/// <summary>One feed as it exists in the Cloudflare database.</summary>
public sealed record D1Feed(string Id, string XmlAddress, string? Title, string? Website, string? IconUrl);

/// <summary>The subscribed subset of a D1 dump, with the counts the import reports.</summary>
public sealed record D1Dump(
    IReadOnlyList<D1Feed> SubscribedFeeds,
    int SkippedUnsubscribed,
    int SubscriptionRows);

/// <summary>What the import actually wrote.</summary>
public sealed record ImportResult(int FeedsImported, int AlreadyPresent, int SkippedUnsubscribed)
{
    public int Kept => FeedsImported + AlreadyPresent;
}
