using Avalonia.Controls;
using Avalonia.Interactivity;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow() => InitializeComponent();

    public OnboardingWindow(OnboardingViewModel model)
        : this() => DataContext = model;

    private void OnDoneClick(object? sender, RoutedEventArgs e)
    {
        (DataContext as OnboardingViewModel)?.Finish();
        Close();
    }
}
