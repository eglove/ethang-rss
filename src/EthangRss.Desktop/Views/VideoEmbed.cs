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

            // WebView2 supplies a real browser identity and origin, which moved the provider from
            // refusing the client outright (error 153) to loading its player at all.
            //
            // Known limitation: the provider also checks the referring page, and this wrapper cannot
            // supply one. Its resource filter is registered only when a handler subscribes before the
            // adapter exists, and the adapter is created on attach, so WebResourceRequested is inert
            // here. Until that is fixed upstream the player reports the video as unavailable, and the
            // Watch on YouTube action inside it is the way out.

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