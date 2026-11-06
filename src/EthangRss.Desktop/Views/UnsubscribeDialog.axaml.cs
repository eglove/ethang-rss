using Avalonia.Controls;
using Avalonia.Interactivity;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Asks before a feed is removed, and says how much goes with it. The answer is a property rather
/// than a dialog result so the view model can read it without knowing what a window is.
/// </summary>
public partial class UnsubscribeDialog : Window
{
    public UnsubscribeDialog() => InitializeComponent();

    public UnsubscribeDialog(string feedTitle, int articleCount)
        : this()
    {
        this.FindControl<TextBlock>("HeadlineText")!.Text = "Unsubscribe from " + feedTitle + "?";
        this.FindControl<TextBlock>("DetailText")!.Text = articleCount switch
        {
            0 => "This feed has no stored articles. It will stop being checked and disappear from the list.",
            1 => "Its 1 stored article will be deleted with it. This cannot be undone.",
            _ => "Its " + articleCount + " stored articles will be deleted with it. This cannot be undone.",
        };
    }

    /// <summary>True only when the person chose to go ahead.</summary>
    public bool IsConfirmed { get; private set; }

    private void OnUnsubscribe(object? sender, RoutedEventArgs eventArgs)
    {
        IsConfirmed = true;
        Close();
    }

    private void OnKeep(object? sender, RoutedEventArgs eventArgs) => Close();
}