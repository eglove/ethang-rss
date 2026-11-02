using EthangRss.Core.Content;

namespace EthangRss.Core.Tests.Content;

public sealed class MediaLinksTests
{
    [Theory]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    public void Recognizes_youtube_video_links(string link)
    {
        var resolved = MediaLinks.Resolve(link);

        Assert.NotNull(resolved);
        Assert.Equal("youtube", resolved!.Provider);
        Assert.Equal("dQw4w9WgXcQ", resolved.Id);
    }

    [Theory]
    [InlineData("https://example.com/blog/post")]
    [InlineData("https://www.youtube.com/feed/trending")]
    [InlineData("https://www.youtube.com/@somechannel")]
    [InlineData("https://youtu.be/short")]
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_everything_that_is_not_a_video(string? link)
    {
        Assert.Null(MediaLinks.Resolve(link));
    }

    [Fact]
    public void The_embed_uses_the_privacy_preserving_domain()
    {
        var video = MediaLinks.Resolve("https://youtu.be/dQw4w9WgXcQ")!;

        Assert.StartsWith("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", video.EmbedUrl, StringComparison.Ordinal);
    }
}
