using EthangRss.Core.Content;

namespace EthangRss.Core.Tests.Content;

public sealed class ArticleExtractorTests
{
    private const string BaseUrl = "https://example.com/posts/one";

    private static string Text(string? html) =>
        html is null ? string.Empty : HtmlContent.Parse(html, BaseUrl).Blocks
            .Select(block => block switch
            {
                ParagraphBlock paragraph => paragraph.PlainText,
                HeadingBlock heading => heading.Text,
                _ => string.Empty,
            })
            .Aggregate(string.Empty, (all, part) => all + " " + part);

    [Fact]
    public void The_article_container_is_chosen_over_the_surrounding_navigation()
    {
        var page = """
            <html><body>
              <nav><a href="/one">Home</a><a href="/two">About</a><a href="/three">Contact</a></nav>
              <article>
                <h1>The real article</h1>
                <p>The first paragraph of the article, which carries a sentence, a comma, and a full stop.</p>
                <p>The second paragraph continues the argument, and it also reads like prose rather than a menu.</p>
              </article>
              <footer><a href="/legal">Legal</a></footer>
            </body></html>
            """;

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.Contains("The real article", extracted, StringComparison.Ordinal);
        Assert.Contains("The second paragraph continues", extracted, StringComparison.Ordinal);
        Assert.DoesNotContain("Legal", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_link_dense_block_is_passed_over_for_the_prose_beside_it()
    {
        var links = string.Concat(Enumerable.Range(0, 40)
            .Select(index => "<li><a href=\"/tag/" + index + "\">A tag name number " + index + "</a></li>"));

        var page = "<html><body>"
            + "<div class=\"tag-cloud\"><ul>" + links + "</ul></div>"
            + "<div class=\"post\"><p>This paragraph is the article itself, and it is written in sentences, with commas.</p>"
            + "<p>A second paragraph of the same article, long enough to be prose rather than a list of links.</p></div>"
            + "</body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.Contains("This paragraph is the article itself", extracted, StringComparison.Ordinal);
        Assert.DoesNotContain("A tag name number", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_with_no_container_falls_back_to_its_paragraphs()
    {
        var page = "<html><body>"
            + "<p>First loose paragraph, which is long enough to count as the article text itself.</p>"
            + "<p>Second loose paragraph, also long enough to be worth keeping in the reading view.</p>"
            + "</body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.Contains("First loose paragraph", extracted, StringComparison.Ordinal);
        Assert.Contains("Second loose paragraph", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void A_page_with_no_prose_yields_nothing_rather_than_a_menu()
    {
        var page = "<html><body><nav><a href=\"/one\">Home</a><a href=\"/two\">About</a></nav>"
            + "<div><a href=\"/three\">Sign in</a></div></body></html>";

        Assert.Null(ArticleExtractor.Extract(page));
    }

    [Fact]
    public void Scripts_styles_and_hidden_boilerplate_are_dropped()
    {
        var page = "<html><head><style>body{color:red}</style></head><body>"
            + "<script>var tracking = 1;</script>"
            + "<article><p>The article text survives, and it is the only thing that should.</p>"
            + "<p>A second sentence follows so the region is comfortably above the minimum length.</p></article>"
            + "</body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.DoesNotContain("tracking", extracted, StringComparison.Ordinal);
        Assert.DoesNotContain("color:red", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void Structure_inside_the_chosen_region_is_kept_for_the_renderer()
    {
        var page = "<html><body><article>"
            + "<h2>A section heading</h2>"
            + "<p>A paragraph under the heading, long enough that the region clears the minimum length.</p>"
            + "<ul><li>first item</li><li>second item</li></ul>"
            + "</article></body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.Contains("<h2", extracted, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<li", extracted, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_quoted_attribute_containing_a_bracket_does_not_leak_into_the_text()
    {
        // Real pages carry Tailwind-style values such as class="[&>a]:hidden", where the '>' ends
        // a naive tag match early and the rest of the attribute arrives as article text.
        var page = "<html><body><article><div class=\"[&>a]:hidden\">"
            + "<p>The article text is what should survive, and this sentence is long enough to count.</p>"
            + "<p>A second paragraph follows so the region clears the minimum length comfortably.</p>"
            + "</div></article></body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.DoesNotContain("hidden", extracted, StringComparison.Ordinal);
        Assert.DoesNotContain("]:", extracted, StringComparison.Ordinal);
        Assert.Contains("The article text is what should survive", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void An_article_inside_a_header_wrapper_is_still_recovered()
    {
        // Some sites put the whole page inside a <header> or <nav> landmark. Deleting those
        // elements outright deletes the article with them, so the wrapper must not decide the
        // outcome - the prose inside it must.
        var page = "<html><body><header class=\"site\">"
            + "<div class=\"post\"><h1>A wrapped article</h1>"
            + "<p>The article sits inside a header landmark, and its sentences are the only prose here.</p>"
            + "<p>A second paragraph makes the region long enough to be chosen over its neighbours.</p>"
            + "</div></header></body></html>";

        var extracted = ArticleExtractor.Extract(page);

        Assert.NotNull(extracted);
        Assert.Contains("The article sits inside a header landmark", extracted, StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_or_missing_page_yields_nothing()
    {
        Assert.Null(ArticleExtractor.Extract(null));
        Assert.Null(ArticleExtractor.Extract(string.Empty));
        Assert.Null(ArticleExtractor.Extract("   "));
    }
}