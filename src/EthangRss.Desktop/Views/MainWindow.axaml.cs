using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using EthangRss.Core.Content;
using EthangRss.Desktop.Services;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Views;

public partial class MainWindow : Window
{
    private readonly ImageLoader _images = new();
    private EthangRss.Core.Subscriptions.FeedSubscriptions? _subscriptions;

    /// <summary>The article the reader pane currently shows, so a redraw only happens for a new one.</summary>
    private long? _renderedArticleId;

    public MainWindow()
    {
        InitializeComponent();

        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? Model => DataContext as MainViewModel;

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (Model is not { } model)
        {
            return;
        }

        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(MainViewModel.ReaderArticleId))
            {
                RenderBodyForCurrentArticle();
            }
        };

        _subscriptions ??= App.Services?.Subscriptions;

        model.OpenUrlRequested += (_, url) => OpenUrl(url);
        model.FocusSearchRequested += (_, _) => this.FindControl<TextBox>("SearchBox")?.Focus();

        RenderBodyForCurrentArticle();
    }

    /// <summary>
    /// Rebuilds the pane only when the article actually changed. Marking an article read re-raises the
    /// body with the same text, and rebuilding then destroys and recreates the native video surface
    /// while it is still starting up - which surfaces as an unhandled abort and takes the app down.
    /// </summary>
    private void RenderBodyForCurrentArticle()
    {
        var articleId = Model?.SelectedArticle?.Id;
        if (articleId == _renderedArticleId)
        {
            return;
        }

        _renderedArticleId = articleId;
        RenderBody();
    }

    private void RenderBody()
    {
        var host = this.FindControl<ContentControl>("ReaderHost");
        if (host is null)
        {
            return;
        }

        var model = Model;
        var html = model?.ReaderBody;
        var body = HtmlContent.Parse(html, model?.ReaderLink ?? "https://ethang.dev");
        var baseUrl = model?.ReaderLink ?? string.Empty;

        host.Content = ArticleRenderer.Build(
            HtmlContent.Parse(html, baseUrl),
            _images,
            OpenUrl,
            baseUrl);
    }

    private void OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (Model is not { } model)
        {
            return;
        }

        var key = eventArgs.Key switch
        {
            Key.J => "j",
            Key.K => "k",
            Key.O => "o",
            Key.S => "s",
            Key.M => "m",
            Key.A => eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "A" : "a",
            Key.U => "u",
            Key.B => "b",
            Key.G => eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "G" : "g",
            Key.R => "r",
            Key.Oem2 => eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "?" : "/",
            Key.Escape => "Escape",
            Key.Enter => "Enter",
            Key.Home => "Home",
            Key.End => "End",
            _ => null,
        };

        if (key is null)
        {
            return;
        }

        var command = KeyboardMap.Resolve(key, eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift), model.IsSearchFocused);
        if (command == ReaderCommand.None)
        {
            return;
        }

        // Let the search box keep typing characters it owns.
        if (!model.IsSearchFocused && command is ReaderCommand.Refresh or ReaderCommand.ToggleStar or ReaderCommand.ToggleRead
            && this.FindControl<TextBox>("SearchBox")?.IsFocused == true)
        {
            return;
        }

        if (model.Run(command))
        {
            eventArgs.Handled = true;
        }
    }

    private void OnOpenClick(object? sender, RoutedEventArgs eventArgs) => Model?.Run(ReaderCommand.OpenInBrowser);

    private void OnStarClick(object? sender, RoutedEventArgs eventArgs) => Model?.Run(ReaderCommand.ToggleStar);

    private void OnToggleReadClick(object? sender, RoutedEventArgs eventArgs) =>
        Model?.Run(ReaderCommand.ToggleRead);

    private void OnMarkAllReadClick(object? sender, RoutedEventArgs eventArgs) =>
        Model?.Run(ReaderCommand.MarkAllRead);

    private void OnShortcutsClick(object? sender, RoutedEventArgs eventArgs) => Model?.Run(ReaderCommand.ShowShortcuts);

    private async void OnSettingsClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (App.Services is not { } services)
        {
            return;
        }

        var model = new SettingsViewModel(services.Settings, services.Database, () => DateTimeOffset.UtcNow);
        var dialog = new SettingsWindow(model, services.Database);
        await dialog.ShowDialog(this);

        Model?.Load();
    }

    private async void OnAddFeedClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainViewModel || _subscriptions is null)
        {
            return;
        }

        var dialog = new AddFeedDialog(_subscriptions);
        var added = await dialog.ShowDialog<long?>(this);
        if (added is not null)
        {
            Model?.Load();
        }
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // A broken link must not take the window down.
        }
    }
}
