namespace EthangRss.Desktop.Services;

/// <summary>The keyboard commands the reader understands. Kept separate from the key itself so
/// bindings are one lookup and the behaviour is testable without a window.</summary>
public enum ReaderCommand
{
    None,
    NextArticle,
    PreviousArticle,
    FirstArticle,
    LastArticle,
    OpenInBrowser,
    ToggleStar,
    ToggleRead,
    MarkAllRead,
    FocusSearch,
    ClearSearch,
    Refresh,
    ShowShortcuts,
    GoToUnread,
    GoToAll,
    GoToStarred,
}

/// <summary>Maps a key press to a command. Text entry wins: while the search box has focus,
/// only Escape and the focus shortcut are honored.</summary>
public static class KeyboardMap
{
    public static ReaderCommand Resolve(string key, bool shift, bool isSearchFocused)
    {
        if (isSearchFocused)
        {
            return key switch
            {
                "Escape" => ReaderCommand.ClearSearch,
                _ => ReaderCommand.None,
            };
        }

        if (key is "?" or "/" && shift)
        {
            return ReaderCommand.ShowShortcuts;
        }

        return key switch
        {
            "j" => ReaderCommand.NextArticle,
            "k" => ReaderCommand.PreviousArticle,
            "/" => ReaderCommand.FocusSearch,
            "Escape" => ReaderCommand.ClearSearch,
            "Enter" => ReaderCommand.OpenInBrowser,
            "o" => ReaderCommand.OpenInBrowser,
            "s" => ReaderCommand.ToggleStar,
            "m" => ReaderCommand.ToggleRead,
            "r" => ReaderCommand.Refresh,
            "g" => ReaderCommand.GoToUnread,
            "G" => ReaderCommand.LastArticle,
            "u" => ReaderCommand.GoToUnread,
            "b" => ReaderCommand.GoToStarred,
            "A" => ReaderCommand.MarkAllRead,
            "Home" => ReaderCommand.FirstArticle,
            "End" => ReaderCommand.LastArticle,
            _ => ReaderCommand.None,
        };
    }

    /// <summary>Human-readable list shown by the shortcut overlay.</summary>
    public static IReadOnlyList<(string Keys, string Description)> CheatSheet { get; } =
    [
        ("j", "next article"),
        ("k", "previous article"),
        ("Home", "first article"),
        ("End", "last article"),
        ("Enter / o", "open in browser"),
        ("s", "star or unstar"),
        ("m", "mark read or unread"),
        ("Shift+A", "mark all read in the current folder"),
        ("u", "jump to unread"),
        ("b", "jump to starred"),
        ("/", "focus search"),
        ("Esc", "clear search"),
        ("r", "refresh feeds now"),
        ("?", "show this list"),
    ];
}
