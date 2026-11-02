namespace EthangRss.Core.Polling;

/// <summary>How polling behaves. Defaults match the spec: 30 minutes, 6 feeds at a time.</summary>
public sealed record PollOptions
{
    public int IntervalMinutes { get; init; } = 30;

    public int MaxConcurrency { get; init; } = 6;
}
