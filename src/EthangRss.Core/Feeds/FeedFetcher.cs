using EthangRss.Core.Polling;

namespace EthangRss.Core.Feeds;

/// <summary>
/// One HTTP surface for every feed request. Never throws for feed-level problems:
/// a bad response is a <see cref="FetchOutcome.Failure"/> the caller records and moves on from.
/// </summary>
public sealed class FeedFetcher : IFeedSource
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How many times one fetch is tried. YouTube's feed endpoint intermittently answers 404 or 500
    /// for a channel that is perfectly alive and serves it on the next request, so a single attempt
    /// turns a transient blip into a permanent-looking error in the feed list. Measured on 20 feeds:
    /// 9 loaded on the first attempt, 12 after three.
    /// </summary>
    public const int MaxAttempts = 3;

    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(400);

    private const string UserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) EthangRss/1.0 (+https://ethang.dev)";

    private readonly HttpClient _client;

    public FeedFetcher(HttpClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        _client = client;
        _client.Timeout = RequestTimeout;
    }

    public async Task<string?> FetchPageAsync(string address, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
            using var response = await _client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode
                ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    public async Task<FetchOutcome> FetchAsync(string xmlAddress, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(xmlAddress, UriKind.Absolute, out var address))
        {
            return FetchOutcome.Failure($"'{xmlAddress}' is not a valid URL.");
        }

        // The last failure is what gets reported; a retry only ever replaces it with a better answer.
        var failure = FetchOutcome.Failure("The feed could not be reached.");

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (attempt > 1)
            {
                await Task.Delay(RetryDelay * (attempt - 1), cancellationToken).ConfigureAwait(false);
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
                    failure = FetchOutcome.Failure(
                        $"The feed responded with {(int)response.StatusCode} {response.ReasonPhrase}.",
                        (int)response.StatusCode);
                    continue;
                }

                var xml = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var parsed = FeedParser.Parse(xml, address);

                if (parsed.IsSuccess)
                {
                    return FetchOutcome.Success(parsed.Feed!);
                }

                // A body that will not parse is not a blip: the same bytes come back every time.
                return FetchOutcome.Failure(parsed.Error ?? "The feed could not be parsed.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                failure = FetchOutcome.Failure("The feed timed out.");
            }
            catch (HttpRequestException exception)
            {
                failure = FetchOutcome.Failure($"The feed could not be reached: {exception.Message}");
            }
        }

        return failure;
    }
}
