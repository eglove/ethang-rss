namespace EthangRss.Desktop.Services;

/// <summary>
/// Ensures one window per user session. A second launch hands focus back to the first rather than
/// starting a second poller over the same database.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private const string MutexName = @"Local\EthangRss.SingleInstance";

    private Mutex? _mutex;

    public bool IsFirstInstance { get; private set; }

    public bool TryClaim()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        IsFirstInstance = createdNew;
        return createdNew;
    }

    public void Dispose()
    {
        if (_mutex is null)
        {
            return;
        }

        try
        {
            if (IsFirstInstance)
            {
                _mutex.ReleaseMutex();
            }
        }
        catch (ApplicationException)
        {
            // Released from a different thread than the owner; nothing to do.
        }
        finally
        {
            _mutex.Dispose();
            _mutex = null;
        }
    }
}
