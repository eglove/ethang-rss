using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;
using EthangRss.Desktop.Views;

namespace EthangRss.Headless.Tests;

public sealed class VideoEmbedHeadlessTests
{
    [AvaloniaFact]
    public void The_embed_builds_a_player_for_a_youtube_watch_link()
    {
        var control = VideoEmbed.TryBuildRaw("https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        Assert.True(control.Built, control.Error ?? "no error reported");
        Assert.NotNull(control.Control);
    }

    [AvaloniaFact]
    public void The_embed_asks_for_the_privacy_domain()
    {
        var control = VideoEmbed.TryBuildRaw("https://youtu.be/dQw4w9WgXcQ");

        Assert.True(control.Built, control.Error ?? "no error reported");
        Assert.Contains("youtube.com/embed/dQw4w9WgXcQ", control.Source!.AbsoluteUri, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void A_plain_article_builds_no_player()
    {
        var control = VideoEmbed.TryBuildRaw("https://example.com/blog/post");

        Assert.False(control.Built);
        Assert.Null(control.Control);
    }
}