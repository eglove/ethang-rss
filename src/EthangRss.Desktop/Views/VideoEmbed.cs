using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform;
using EthangRss.Core.Content;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Plays a video article. The player is a real Edge WebView2 surface, pinned deliberately: it is the
/// only Windows implementation with a genuine browser identity and origin, which the provider checks
/// before it will serve a playable stream.
/// </summary>
public static class VideoEmbed
{
    private static readonly Uri OriginatingPage = new("https://www.youtube.com/");

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
                AdapterPreference = [WebViewAdapterType.WebView2],
            };

            // Only the missing referrer is supplied. The surface already carries a real browser
            // origin from WebView2, and overriding Origin makes the provider reject the request.
            view.WebResourceRequested += (_, eventArgs) =>
                eventArgs.Request.Headers.TrySet("Referer", OriginatingPage.ToString());

            view.Source = source;
            return new BuildAttempt(view, source, null);
        }
        catch (Exception exception)
        {
            return new BuildAttempt(null, source, exception.GetType().Name + ": " + exception.Message);
        }
    }

    public static string DescribeAdapter(Control? control) =>
        control is NativeWebView web ? web.AdapterInfo?.ToString() ?? "pending" : "not a web view";

    public static bool IsVideo(string? articleLink) => MediaLinks.Resolve(articleLink)?.EmbedUrl is not null;
}
