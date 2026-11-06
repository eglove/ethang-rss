using EthangRss.Core.Formatting;
using EthangRss.Desktop.ViewModels;

namespace EthangRss.Desktop.Tests.Formatting;

public sealed class ArticleRowVmTests
{
    private static ArticleRowVm Row(DateTimeOffset? published) =>
        new(1, "Title", "A feed", null, false, false, published, null);

    [Fact]
    public void The_row_carries_a_locale_formatted_timestamp()
    {
        var published = new DateTimeOffset(2026, 3, 4, 15, 30, 0, TimeSpan.Zero);
        var row = Row(published);

        Assert.True(row.HasPublishedLabel);
        Assert.Equal(LocaleDate.Format(published), row.PublishedLabel);
    }

    [Fact]
    public void A_row_without_a_timestamp_shows_no_label()
    {
        var row = Row(null);

        Assert.False(row.HasPublishedLabel);
        Assert.Equal(string.Empty, row.PublishedLabel);
    }
}
