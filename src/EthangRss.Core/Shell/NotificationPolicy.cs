namespace EthangRss.Core.Shell;

/// <summary>
/// Decides whether a poll is worth interrupting the user for. Notifications are off by default and
/// batched: one message per poll, never one per article.
/// </summary>
public static class NotificationPolicy
{
    public static NotificationDecision Decide(bool enabled, int threshold, int newArticles)
    {
        if (!enabled)
        {
            return NotificationDecision.None;
        }

        var minimum = Math.Max(1, threshold);
        if (newArticles < minimum)
        {
            return NotificationDecision.None;
        }

        return NotificationDecision.Show(
            newArticles == 1 ? "1 new article" : newArticles + " new articles");
    }
}

public sealed record NotificationDecision(bool ShouldShow, string Message)
{
    public static NotificationDecision None => new(false, string.Empty);

    public static NotificationDecision Show(string message) => new(true, message);
}
