using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using EthangRss.Core.Content;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Plays a video article in place of a body. The player is a real embedded web surface restricted to
/// the video host, so nothing else can navigate inside it.
/// </summary>
public static class VideoEmbed
{
    /// <summary>Returns the player, or null when this article is not a video we can embed.</summary>
    public static Control? TryBuild(string? articleLink)
    {
        var video = MediaLinks.Resolve(articleLink);
        if (video?.EmbedUrl is not { } embedUrl)
        {
            return null;
        }

        try
        {
            var view = new NativeWebView
            {
                Height = 420,
                HorizontalAlignment = HorizontalAlignment.Stretch,
            };

            view.Source = new Uri(embedUrl);
            return view;
        }
        catch (Exception)
        {
            // A machine without the web view runtime still gets a readable article list.
            return null;
        }
    }

    /// <summary>True when this article will be played rather than read.</summary>
    public static bool IsVideo(string? articleLink) => MediaLinks.Resolve(articleLink)?.EmbedUrl is not null;
}
