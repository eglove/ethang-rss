namespace EthangRss.Core;

/// <summary>Every settings key the app reads or writes, in one place so they cannot drift.</summary>
public static class SettingKeys
{
    public const string RefreshIntervalMinutes = "RefreshIntervalMinutes";
    public const string KeepArticlesForever = "KeepArticlesForever";
    public const string RetentionDays = "RetentionDays";
    public const string FilterYoutubeShorts = "FilterYoutubeShorts";
    public const string NotificationsEnabled = "NotificationsEnabled";
    public const string StartWithWindows = "StartWithWindows";
    public const string OnboardingComplete = "OnboardingComplete";
    public const string LastPolledAt = "LastPolledAt";
    public const string WindowWidth = "WindowWidth";
    public const string WindowHeight = "WindowHeight";
    public const string NavigationWidth = "NavigationWidth";
    public const string ListWidth = "ListWidth";
    public const string PageSize = "PageSize";
}
