using EthangRss.Core.Content;

namespace EthangRss.Core.Tests.Content;

public sealed class HtmlContentTests
{
    private const string BaseUrl = "https://example.com/posts/one";

    private static ArticleBody Parse(string html) => HtmlContent.Parse(html, BaseUrl);

    [Fact]
    public void Paragraphs_become_paragraph_blocks()
    {
        var body = Parse("<p>First paragraph.</p><p>Second paragraph.</p>");

        Assert.Equal(2, body.Blocks.Count);
        var first = Assert.IsType<ParagraphBlock>(body.Blocks[0]);
        Assert.Equal("First paragraph.", first.PlainText);
    }

    [Fact]
    public void Scripts_and_styles_are_stripped()
    {
        var body = Parse("<script>alert('x')</script><style>p{color:red}</style><p>Visible</p>");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        Assert.Equal("Visible", block.PlainText);
    }

    [Fact]
    public void Bold_and_italic_runs_are_kept()
    {
        var body = Parse("<p>plain <strong>bold</strong> and <em>italic</em></p>");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        Assert.Contains(block.Runs, run => run is { Text: "bold", IsBold: true });
        Assert.Contains(block.Runs, run => run is { Text: "italic", IsItalic: true });
    }

    [Fact]
    public void Links_capture_their_target()
    {
        var body = Parse("<p>see <a href=\"/docs/page\">the docs</a></p>");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        var link = Assert.Single(block.Runs, run => run.Href is not null);
        Assert.Equal("the docs", link.Text);
        Assert.Equal("https://example.com/docs/page", link.Href);
    }

    [Fact]
    public void Relative_image_sources_are_made_absolute()
    {
        var body = Parse("<p><img src=\"/img/cover.png\" alt=\"Cover\"></p>");

        var image = Assert.IsType<ImageBlock>(Assert.Single(body.Blocks));
        Assert.Equal("https://example.com/img/cover.png", image.Url);
        Assert.Equal("Cover", image.Alt);
    }

    [Fact]
    public void Headings_keep_their_level()
    {
        var body = Parse("<h2>A section</h2><p>text</p>");

        var heading = Assert.IsType<HeadingBlock>(body.Blocks[0]);
        Assert.Equal(2, heading.Level);
        Assert.Equal("A section", heading.Text);
    }

    [Fact]
    public void Unordered_and_ordered_lists_are_distinguished()
    {
        var unordered = Parse("<ul><li>one</li><li>two</li></ul>");
        var ordered = Parse("<ol><li>one</li><li>two</li></ol>");

        var list = Assert.IsType<ListBlock>(Assert.Single(unordered.Blocks));
        Assert.False(list.IsOrdered);
        Assert.Equal(new[] { "one", "two" }, list.Items);

        Assert.True(Assert.IsType<ListBlock>(Assert.Single(ordered.Blocks)).IsOrdered);
    }

    [Fact]
    public void Pre_blocks_survive_verbatim()
    {
        var body = Parse("<pre><code>var x = 1;</code></pre>");

        var code = Assert.IsType<CodeBlock>(Assert.Single(body.Blocks));
        Assert.Contains("var x = 1;", code.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Blockquotes_nest_their_content()
    {
        var body = Parse("<blockquote><p>quoted</p></blockquote>");

        var quote = Assert.IsType<QuoteBlock>(Assert.Single(body.Blocks));
        var paragraph = Assert.IsType<ParagraphBlock>(Assert.Single(quote.Children));
        Assert.Equal("quoted", paragraph.PlainText);
    }

    [Fact]
    public void Html_entities_are_decoded()
    {
        var body = Parse("<p>Tom &amp; Jerry &lt;3 &#8220;quotes&#8221;</p>");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        Assert.Contains("Tom & Jerry", block.PlainText, StringComparison.Ordinal);
        Assert.DoesNotContain("&amp;", block.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void Consecutive_paragraphs_stay_separate_blocks()
    {
        // The block matcher is anchored, so a tag that is not at the very start of the remaining
        // text used to be skipped and the paragraphs after it ran together into one block.
        var body = Parse("<p>One.</p><p>Two.</p><p>Three.</p>");

        Assert.Equal(3, body.Blocks.Count);
        Assert.Equal(new[] { "One.", "Two.", "Three." },
            body.Blocks.Cast<ParagraphBlock>().Select(block => block.PlainText));
    }

    [Fact]
    public void Whitespace_between_blocks_does_not_become_a_paragraph()
    {
        // Real feed bodies are full of blank lines between tags. A run that is only whitespace is
        // not a paragraph, and rendering it as one puts an empty gap in the article.
        var body = Parse("<p>One.</p>\n\n\n<p>Two.</p>");

        Assert.Equal(2, body.Blocks.Count);
        Assert.All(body.Blocks.Cast<ParagraphBlock>(), block =>
            Assert.False(string.IsNullOrWhiteSpace(block.PlainText)));
    }

    [Fact]
    public void A_paragraph_followed_by_a_heading_keeps_both()
    {
        var body = Parse("<p>Intro.</p><h2>A section</h2><p>Body.</p>");

        Assert.Equal(3, body.Blocks.Count);
        Assert.IsType<ParagraphBlock>(body.Blocks[0]);
        Assert.Equal("A section", Assert.IsType<HeadingBlock>(body.Blocks[1]).Text);
        Assert.Equal("Body.", Assert.IsType<ParagraphBlock>(body.Blocks[2]).PlainText);
    }

    [Fact]
    public void A_quoted_attribute_containing_a_bracket_does_not_leak_into_the_text()
    {
        // Tailwind-style values such as class="[&>a]:hidden" put a '>' inside a quoted attribute.
        // A tag match that stops at the first '>' ends mid-attribute and the rest arrives as text.
        var body = Parse("<p><span class=\"[&>a]:hidden\">Visible words only</span></p>");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        Assert.Equal("Visible words only", block.PlainText);
    }

    [Fact]
    public void Plain_text_without_markup_still_produces_a_paragraph()
    {
        var body = Parse("Just words with no tags.");

        var block = Assert.IsType<ParagraphBlock>(Assert.Single(body.Blocks));
        Assert.Equal("Just words with no tags.", block.PlainText);
    }

    [Fact]
    public void Empty_or_whitespace_content_yields_no_blocks()
    {
        Assert.True(Parse("").IsEmpty);
        Assert.True(Parse("   \n  ").IsEmpty);
    }

    [Fact]
    public void Deeply_nested_markup_does_not_throw()
    {
        var html = string.Concat(Enumerable.Repeat("<div>", 200)) + "<p>deep</p>" + string.Concat(Enumerable.Repeat("</div>", 200));

        var exception = Record.Exception(() => Parse(html));

        Assert.Null(exception);
    }
}
