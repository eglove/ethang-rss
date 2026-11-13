using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Decides which link, if any, sits under a point in a rendered paragraph. A paragraph is a
/// TextBlock of inline runs, and only the runs that carry an address are a target: treating the
/// whole block as clickable makes every sentence in an article that cites one report open that
/// report.
/// </summary>
public static class LinkHit
{
    /// <summary>
    /// The linked runs of one block, in the order they appear. A Run carries no address of its own,
    /// so the renderer records them here when it builds the paragraph.
    /// </summary>
    private static readonly ConditionalWeakTable<TextBlock, Dictionary<Run, string>> Links = new();

    /// <summary>Records that <paramref name="run"/> points at <paramref name="href"/>.</summary>
    public static void Register(TextBlock text, Run run, string href)
    {
        var map = Links.GetOrCreateValue(text);
        map[run] = href;
    }

    /// <summary>
    /// The address of the linked words under <paramref name="point"/>, in the block's own
    /// coordinates, or null when the point is over prose.
    /// </summary>
    public static string? HrefAt(TextBlock text, Point point)
    {
        if (text.Inlines is null || text.TextLayout is null || !Links.TryGetValue(text, out var map))
        {
            return null;
        }

        // Which character the point falls on. A point outside the text is not on any character,
        // which is what stops a click in the margin from opening the last link on the line.
        var hit = text.TextLayout.HitTestPoint(point);
        if (!hit.IsInside)
        {
            return null;
        }

        var index = hit.TextPosition;

        var position = 0;
        foreach (var run in text.Inlines.OfType<Run>())
        {
            var length = run.Text?.Length ?? 0;
            if (index < position + length)
            {
                return map.TryGetValue(run, out var href) ? href : null;
            }

            position += length;
        }

        return null;
    }
}