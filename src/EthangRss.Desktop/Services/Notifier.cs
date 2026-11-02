using EthangRss.Core.Shell;

namespace EthangRss.Desktop.Services;

/// <summary>
/// Desktop notifications. The display mechanism is supplied once a tray surface exists; without
/// one the app stays silent rather than throwing.
/// </summary>
public sealed class Notifier
{
    private readonly Func<bool> _enabled;
    private readonly Func<int> _threshold;
    private Action<string, string>? _show;

    public Notifier(Func<bool> enabled, Func<int> threshold)
    {
        _enabled = enabled;
        _threshold = threshold;
    }

    public void Attach(Action<string, string> show) => _show = show;

    public NotificationDecision Notify(int newArticles)
    {
        var decision = NotificationPolicy.Decide(_enabled(), _threshold(), newArticles);
        if (decision.ShouldShow)
        {
            _show?.Invoke("Ethang RSS", decision.Message);
        }

        return decision;
    }
}
