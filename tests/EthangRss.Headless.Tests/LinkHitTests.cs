using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using EthangRss.Core.Content;
using EthangRss.Desktop.Views;
using Xunit;

namespace EthangRss.Headless.Tests;

/// <summary>
/// A paragraph that mentions a link is mostly prose. Only the linked words are a target: the pane
/// used to make the whole paragraph clickable, so every sentence in an article that cited one report
/// opened that report. The decision is a function of the paragraph and a point, which is what these
/// tests pin down.
/// </summary>
public sealed class LinkHitTests
{
    private const string BaseUrl = "https://example.com/posts/one";

    private static TextBlock Build(string html)
    {
        var paragraph = HtmlContent.Parse(html, BaseUrl).Blocks.OfType<ParagraphBlock>().Single();
        var block = ArticleRenderer.BuildParagraph(paragraph, _ => { });

        // Layout needs a live visual tree, so the block is shown in a window before it is measured.
        var window = new Window { Width = 900, Height = 200, Content = block };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        return block;
    }

    private static Point PointOf(TextBlock text, int index)
    {
        var box = text.TextLayout.HitTestTextRange(index, 1).First();
        return new Point(box.X + (box.Width / 2), box.Y + (box.Height / 2));
    }

    private static int Length(TextBlock text) =>
        text.Inlines!.OfType<Run>().Sum(run => run.Text?.Length ?? 0);

    [AvaloniaFact]
    public void A_point_over_the_prose_has_no_link()
    {
        var text = Build(
            "<p>The findings are now available in our new report, which covers nine case studies " +
            "and a great deal of practitioner detail, <a href=\"https://example.com/report\">How Top Teams Scale</a>.</p>");

        Assert.Null(LinkHit.HrefAt(text, PointOf(text, 0)));
        Assert.Null(LinkHit.HrefAt(text, PointOf(text, 40)));
    }

    [AvaloniaFact]
    public void A_point_over_the_linked_words_has_the_link()
    {
        var text = Build("<p>Short intro <a href=\"https://example.com/report\">the report</a></p>");

        var index = Length(text) - 6;
        Assert.Equal("https://example.com/report", LinkHit.HrefAt(text, PointOf(text, index)));
    }

    [AvaloniaFact]
    public void A_paragraph_with_no_links_has_none_anywhere()
    {
        var text = Build("<p>Plain prose with no links at all in it.</p>");

        Assert.Null(LinkHit.HrefAt(text, PointOf(text, 0)));
        Assert.Null(LinkHit.HrefAt(text, PointOf(text, 10)));
    }

    [AvaloniaFact]
    public void A_point_past_the_end_of_the_text_has_no_link()
    {
        var text = Build("<p>Short intro <a href=\"https://example.com/report\">the report</a></p>");

        Assert.Null(LinkHit.HrefAt(text, new Point(5000, 5000)));
    }
}