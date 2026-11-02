using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using EthangRss.Core.Content;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

public sealed class RendererHeadlessTests
{
    [AvaloniaFact]
    public void A_headline_only_video_article_renders_a_player_not_the_notice()
    {
        var body = HtmlContent.Parse(string.Empty, "https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        var control = ArticleRenderer.Build(body, null, _ => { }, "https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        var panel = Assert.IsType<StackPanel>(control);
        var kinds = string.Join(",", panel.Children.Select(c => c.GetType().Name));

        Assert.True(
            panel.Children.Any(c => c is NativeWebView),
            "expected a player, got: " + kinds);
    }

    [AvaloniaFact]
    public void A_headline_only_text_article_says_so_without_a_dead_button()
    {
        var body = HtmlContent.Parse(string.Empty, "https://example.com/post");

        var control = ArticleRenderer.Build(body, null, _ => { }, "https://example.com/post");

        var panel = Assert.IsType<StackPanel>(control);
        Assert.DoesNotContain(panel.Children, c => c is Button);
        Assert.Contains(panel.Children.OfType<TextBlock>(), t =>
            (t.Text ?? string.Empty).Contains("headlines without article text", StringComparison.Ordinal));
    }

    [AvaloniaFact]
    public void A_video_article_with_a_body_still_plays_the_video()
    {
        var body = HtmlContent.Parse("<p>Show notes here</p>", "https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        var control = ArticleRenderer.Build(body, null, _ => { }, "https://www.youtube.com/watch?v=k5ybiDRnR5Y");

        var panel = Assert.IsType<StackPanel>(control);
        Assert.Contains(panel.Children, c => c is NativeWebView);
    }
}
