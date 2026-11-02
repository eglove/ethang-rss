namespace EthangRss.Core.Feeds;

/// <summary>
/// One HTTP surface for every feed request. Never throws for feed-level problems:
/// a bad response is a <see cref="FetchOutcome.Failure"/> the caller records and moves on from.
/// </summary>
public sealed class FeedFetcher
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EthangRss/1.0 (+https://ethang.dev)";

    private readonly HttpClient _client;

    public FeedFetcher(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _client.Timeout = RequestTimeout;
    }

    public async Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(xmlAddress, UriKind.Absolute, out var address))
        {
            return FetchOutcome.Failure($"'{xmlAddress}' is not a valid URL.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            request.Headers.TryAddWithoutValidation(
                "Accept",
                "application/rss+xml, application/atom+xml, application/xml, text/xml, */*");

            using var response = await _client
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return FetchOutcome.Failure(
                    $"The feed responded with {(int)response.StatusCode} {response.ReasonPhrase}.",
                    (int)response.StatusCode);
            }

            var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var parsed = FeedParser.Parse(xml, address);

            return parsed.IsSuccess
                ? FetchOutcome.Success(parsed.Feed!)
                : FetchOutcome.Failure(parsed.Error ?? "The feed could not be parsed.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return FetchOutcome.Failure("The feed timed out.");
        }
        catch (HttpRequestException exception)
        {
            return FetchOutcome.Failure($"The feed could not be reached: {exception.Message}");
        }
    }
}
