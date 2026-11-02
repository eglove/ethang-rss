using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Views;

public partial class OnboardingWindow : Window
{
    public OnboardingWindow() => InitializeComponent();

    public OnboardingWindow(OnboardingViewModel model)
        : this() => DataContext = model;

    private OnboardingViewModel? Model => DataContext as OnboardingViewModel;

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (Model is null)
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose the exported .sql file",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("SQL dump") { Patterns = ["*.sql"] }],
        });

        if (files.Count > 0 && files[0].TryGetLocalPath() is { } path)
        {
            Model.DumpPath = path;
        }
    }

    private void OnImportClick(object? sender, RoutedEventArgs e) => Model?.Import();

    private void OnStartEmptyClick(object? sender, RoutedEventArgs e) => Model?.StartEmpty();

    private void OnDoneClick(object? sender, RoutedEventArgs e) => Close();
}
