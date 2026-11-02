using EthangRss.Core.Shell;

namespace EthangRss.Core.Tests.Shell;

public sealed class ShellHelperTests
{
    [Fact]
    public void Notifications_are_suppressed_when_disabled()
    {
        Assert.False(NotificationPolicy.Decide(enabled: false, threshold: 1, newArticles: 50).ShouldShow);
    }

    [Fact]
    public void Below_the_threshold_stays_quiet()
    {
        Assert.False(NotificationPolicy.Decide(enabled: true, threshold: 5, newArticles: 4).ShouldShow);
    }

    [Fact]
    public void Meeting_the_threshold_shows_one_batched_message()
    {
        var decision = NotificationPolicy.Decide(enabled: true, threshold: 5, newArticles: 12);

        Assert.True(decision.ShouldShow);
        Assert.Equal("12 new articles", decision.Message);
    }

    [Fact]
    public void A_single_article_reads_naturally()
    {
        Assert.Equal("1 new article", NotificationPolicy.Decide(true, 1, 1).Message);
    }

    [Fact]
    public void A_nonsense_threshold_still_means_at_least_one()
    {
        Assert.True(NotificationPolicy.Decide(true, 0, 1).ShouldShow);
        Assert.False(NotificationPolicy.Decide(true, -3, 0).ShouldShow);
    }

    [Fact]
    public void The_autostart_command_quotes_the_executable_path()
    {
        Assert.Equal(
            @"""C:\Program Files\EthangRss\EthangRss.exe""",
            AutoStart.BuildCommand(@"C:\Program Files\EthangRss\EthangRss.exe"));
    }

    [Fact]
    public void Already_quoted_paths_are_not_double_quoted()
    {
        Assert.Equal(@"""C:\apps\EthangRss.exe""", AutoStart.BuildCommand(@"""C:\apps\EthangRss.exe"""));
    }

    [Fact]
    public void The_registry_location_is_the_per_user_run_key()
    {
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Run", AutoStart.RunKeyPath);
        Assert.Equal("EthangRss", AutoStart.ValueName);
    }
}
