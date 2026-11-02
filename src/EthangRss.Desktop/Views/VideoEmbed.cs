using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using EthangRss.Core.Content;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Plays a video article in place of a body. The player is a real embedded web surface showing a
/// local page that frames the provider's player, which is the arrangement the provider expects;
/// loading the embed URL directly gets a configuration error instead.
/// </summary>
public static class VideoEmbed
{
    private const string DesktopUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/140.0.0.0 Safari/537.36";

    public sealed record BuildAttempt(Control? Control, Uri? Source, string? Error)
    {
        public bool Built => Control is not null;
    }

    public static Control? TryBuild(string? articleLink) => TryBuildRaw(articleLink).Control;

    public static BuildAttempt TryBuildRaw(string? articleLink)
    {
        var video = MediaLinks.Resolve(articleLink);
        if (video?.EmbedUrl is not { } embedUrl)
        {
            return new BuildAttempt(null, null, "not a video link");
        }

        var source = new Uri(embedUrl);

        try
        {
            var view = new NativeWebView
            {
                Height = 420,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                UserAgent = DesktopUserAgent,
            };

            var page = LoadPlayerPage(video);
            if (page is not null)
            {
                view.NavigateToString(page, new Uri("https://www.youtube.com/"));
            }
            else
            {
                view.Source = source;
            }

            return new BuildAttempt(view, source, null);
        }
        catch (Exception exception)
        {
            return new BuildAttempt(null, source, exception.GetType().Name + ": " + exception.Message);
        }
    }

    private static string? LoadPlayerPage(VideoReference video)
    {
        try
        {
            using var stream = AssetLoader.Open(new Uri("avares://EthangRss/Assets/player.html"));
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd().Replace("__ID__", video.Id, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsVideo(string? articleLink) => MediaLinks.Resolve(articleLink)?.EmbedUrl is not null;
}
