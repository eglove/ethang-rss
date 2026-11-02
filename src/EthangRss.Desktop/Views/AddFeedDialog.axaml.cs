using Avalonia.Controls;
using Avalonia.Interactivity;
using EthangRss.Core.Subscriptions;

namespace EthangRss.Desktop.Views;

public partial class AddFeedDialog : Window
{
    private FeedSubscriptions? _subscriptions;

    public AddFeedDialog() => InitializeComponent();

    public AddFeedDialog(FeedSubscriptions subscriptions)
        : this()
    {
        _subscriptions = subscriptions;
        Opened += (_, _) => this.FindControl<TextBox>("AddressBox")?.Focus();
    }

    private async void OnAdd(object? sender, RoutedEventArgs eventArgs)
    {
        var address = this.FindControl<TextBox>("AddressBox")?.Text?.Trim();
        var status = this.FindControl<TextBlock>("StatusText");
        var button = this.FindControl<Button>("AddButton");

        if (string.IsNullOrWhiteSpace(address) || _subscriptions is null || status is null)
        {
            return;
        }

        if (button is not null)
        {
            button.IsEnabled = false;
        }

        status.Text = "Looking for a feed...";

        try
        {
            var result = await _subscriptions.AddAsync(address, CancellationToken.None);
            status.Text = result.Message;

            if (result.Success)
            {
                Close(result.FeedId);
                return;
            }
        }
        catch (Exception exception)
        {
            status.Text = "Could not add that feed: " + exception.Message;
        }

        if (button is not null)
        {
            button.IsEnabled = true;
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs eventArgs) => Close(null);
}
