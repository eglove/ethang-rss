using EthangRss.Core.Content;
using EthangRss.Desktop.Views;

namespace EthangRss.Desktop.Tests;

public sealed class VideoEmbedTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    public void Video_articles_are_recognized_for_embedding(string link)
    {
        Assert.True(VideoEmbed.IsVideo(link));
    }

    [Theory]
    [InlineData("https://example.com/blog/post")]
    [InlineData(null)]
    public void Ordinary_articles_are_not_treated_as_video(string? link)
    {
        Assert.False(VideoEmbed.IsVideo(link));
    }

    [Fact]
    public void The_embed_url_points_at_the_youtube_player()
    {
        var video = MediaLinks.Resolve("https://www.youtube.com/watch?v=dQw4w9WgXcQ")!;

        Assert.Contains("youtube-nocookie.com/embed/dQw4w9WgXcQ", video.EmbedUrl!, StringComparison.Ordinal);
    }
}
