using EthangRss.Core.Feeds;

namespace EthangRss.Core.Polling;

/// <summary>The one thing the scheduler needs from the network, so tests can replace it.</summary>
public interface IFeedSource
{
    Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken);
}
