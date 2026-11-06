using System.Globalization;
using EthangRss.Core.Formatting;

namespace EthangRss.Core.Tests.Formatting;

public sealed class LocaleDateTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 3, 4, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void A_missing_timestamp_renders_as_nothing()
    {
        Assert.Equal(string.Empty, LocaleDate.Format(null));
    }

    [Fact]
    public void The_pattern_comes_from_the_culture_rather_than_a_fixed_string()
    {
        var us = LocaleDate.Format(Timestamp, new CultureInfo("en-US"));
        var uk = LocaleDate.Format(Timestamp, new CultureInfo("en-GB"));
        var de = LocaleDate.Format(Timestamp, new CultureInfo("de-DE"));

        // Same instant, three locales, three readings: none is a hard-coded pattern.
        Assert.Equal(Timestamp.ToLocalTime().ToString("g", new CultureInfo("en-US")), us);
        Assert.Equal(Timestamp.ToLocalTime().ToString("g", new CultureInfo("en-GB")), uk);
        Assert.Equal(Timestamp.ToLocalTime().ToString("g", new CultureInfo("de-DE")), de);
        Assert.NotEqual(us, de);
    }

    [Fact]
    public void The_timestamp_is_shown_in_local_time()
    {
        var culture = new CultureInfo("en-GB");
        var expected = Timestamp.ToLocalTime().ToString("g", culture);

        Assert.Equal(expected, LocaleDate.Format(Timestamp, culture));
        Assert.DoesNotContain("Z", LocaleDate.Format(Timestamp, culture));
    }
}
