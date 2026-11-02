using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Platform;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

public sealed class VideoEmbedHeadlessTests
{
    [AvaloniaFact]
    public void The_embed_builds_a_player_for_a_youtube_watch_link()
    {
        var attempt = VideoEmbed.TryBuildRaw("https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        Assert.True(attempt.Built, attempt.Error ?? "no error reported");
    }

    [AvaloniaFact]
    public void The_player_is_pinned_to_the_webview2_adapter()
    {
        var attempt = VideoEmbed.TryBuildRaw("https://www.youtube.com/watch?v=k5ybiDRnR5Y");
        var view = Assert.IsType<NativeWebView>(attempt.Control);

        Assert.Equal([WebViewAdapterType.WebView2], view.AdapterPreference!);
    }

    [AvaloniaFact]
    public void The_player_is_asked_for_the_video_embed_address()
    {
        var attempt = VideoEmbed.TryBuildRaw("https://youtu.be/dQw4w9WgXcQ");

        Assert.True(attempt.Built, attempt.Error ?? "no error reported");
        Assert.Contains("youtube.com/embed/dQw4w9WgXcQ", attempt.Source!.AbsoluteUri, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void A_plain_article_builds_no_player()
    {
        var attempt = VideoEmbed.TryBuildRaw("https://example.com/blog/post");

        Assert.False(attempt.Built);
        Assert.Null(attempt.Control);
    }
}
