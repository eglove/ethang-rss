namespace EthangRss.Core.Polling;

/// <summary>What a poll pass did. Accumulates across feeds as each one finishes.</summary>
public sealed record PollSummary
{
    public int Polled { get; init; }

    public int Succeeded { get; init; }

    public int Failed { get; init; }

    public int NewArticles { get; init; }

    public static PollSummary Empty => new();
}
