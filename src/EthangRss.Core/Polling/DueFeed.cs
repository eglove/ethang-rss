namespace EthangRss.Core.Polling;

internal sealed record DueFeed(long Id, string XmlAddress, string? LastFetchedAt, int? FetchIntervalMinutes);
