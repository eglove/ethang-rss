using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using EthangRss.Core.Content;

namespace EthangRss.Desktop.Views;

/// <summary>
/// Draws a parsed article body with real controls instead of embedding a browser. Anything the
/// parser could not classify arrives as plain text, so nothing is silently lost.
/// </summary>
public static class ArticleRenderer
{
    public static Control Build(ArticleBody body, IImageLoader? images, Action<string> openUrl, string? sourceLink = null)
    {
        var panel = new StackPanel { Spacing = 14 };

        if (body.IsEmpty)
        {
            // A video article has no body worth reading; play it instead.
            if (VideoEmbed.TryBuild(sourceLink) is { } player)
            {
                panel.Children.Add(player);
                return panel;
            }

            // Otherwise there is genuinely no content: say so once, plainly, and leave the
            // toolbar's Open in browser as the only way out.
            panel.Children.Add(new TextBlock
            {
                Text = "This feed publishes headlines without article text.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Gray,
                FontSize = 14,
            });

            return panel;
        }

        foreach (var block in body.Blocks)
        {
            var control = BuildBlock(block, images, openUrl);
            if (control is not null)
            {
                panel.Children.Add(control);
            }
        }

        return panel;
    }

    private static Control? BuildBlock(ContentBlock block, IImageLoader? images, Action<string> openUrl)
    {
        switch (block)
        {
            case HeadingBlock heading:
                return new TextBlock
                {
                    Text = heading.Text,
                    TextWrapping = TextWrapping.Wrap,
                    FontWeight = FontWeight.SemiBold,
                    FontSize = heading.Level switch { 1 => 24, 2 => 20, _ => 17 },
                    Margin = new Thickness(0, 8, 0, 0),
                };

            case ParagraphBlock paragraph:
                return BuildParagraph(paragraph, openUrl);

            case ListBlock list:
            {
                var stack = new StackPanel { Spacing = 6 };
                var index = 1;
                foreach (var item in list.Items)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = (list.IsOrdered ? index + ". " : "\u2022  ") + item,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(12, 0, 0, 0),
                    });
                    index++;
                }

                return stack;
            }

            case CodeBlock code:
                return new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#0D1015")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#252B36")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(12),
                    Child = new TextBlock
                    {
                        Text = code.Text,
                        FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
                        FontSize = 13,
                        TextWrapping = TextWrapping.Wrap,
                    },
                };

            case ImageBlock image:
                return BuildImage(image, images);

            case SeparatorBlock:
                return new Border
                {
                    Height = 1,
                    Background = new SolidColorBrush(Color.Parse("#252B36")),
                    Margin = new Thickness(0, 6, 0, 6),
                };

            case QuoteBlock quote:
            {
                var inner = new StackPanel { Spacing = 8 };
                foreach (var child in quote.Children)
                {
                    var control = BuildBlock(child, images, openUrl);
                    if (control is not null)
                    {
                        inner.Children.Add(control);
                    }
                }

                return new Border
                {
                    BorderBrush = new SolidColorBrush(Color.Parse("#7C5CFF")),
                    BorderThickness = new Thickness(3, 0, 0, 0),
                    Padding = new Thickness(14, 4, 0, 4),
                    Child = inner,
                };
            }

            default:
                return null;
        }
    }

    private static Control BuildParagraph(ParagraphBlock paragraph, Action<string> openUrl)
    {
        var text = new TextBlock
        {
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 24,
            FontSize = 15,
        };

        foreach (var run in paragraph.Runs)
        {
            var inline = new Run(run.Text)
            {
                FontWeight = run.IsBold ? FontWeight.SemiBold : FontWeight.Normal,
                FontStyle = run.IsItalic ? FontStyle.Italic : FontStyle.Normal,
                FontFamily = run.IsCode ? new FontFamily("Cascadia Mono, Consolas, monospace") : FontFamily.Default,
            };

            if (run.Href is { Length: > 0 })
            {
                inline.Foreground = new SolidColorBrush(Color.Parse("#A58BFF"));
                inline.TextDecorations = TextDecorations.Underline;
            }

            text.Inlines?.Add(inline);
        }

        // Links are clickable through a single scripted handler rather than one TextBlock per run.
        var href = paragraph.Runs.FirstOrDefault(run => run.Href is not null)?.Href;
        if (href is not null)
        {
            text.PointerPressed += (_, _) => openUrl(href);
            text.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
        }

        return text;
    }

    private static Control BuildImage(ImageBlock image, IImageLoader? images)
    {
        var container = new StackPanel { Spacing = 4 };

        if (images is not null)
        {
            var control = new Image
            {
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left,
                MaxHeight = 420,
            };

            images.Load(image.Url, bitmap => control.Source = bitmap);
            container.Children.Add(control);
        }

        if (image.Alt.Length > 0)
        {
            container.Children.Add(new TextBlock
            {
                Text = image.Alt,
                FontSize = 12,
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
            });
        }

        return container;
    }
}

/// <summary>Loads remote images so the reader can show them inline.</summary>
public interface IImageLoader
{
    void Load(string url, Action<Bitmap> onLoaded);
}