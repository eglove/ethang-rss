using EthangRss.Desktop.Services;

namespace EthangRss.Desktop.Tests;

public sealed class KeyboardMapTests
{
    [Theory]
    [InlineData("j", ReaderCommand.NextArticle)]
    [InlineData("k", ReaderCommand.PreviousArticle)]
    [InlineData("s", ReaderCommand.ToggleStar)]
    [InlineData("m", ReaderCommand.ToggleRead)]
    [InlineData("r", ReaderCommand.Refresh)]
    [InlineData("u", ReaderCommand.GoToUnread)]
    [InlineData("b", ReaderCommand.GoToStarred)]
    [InlineData("/", ReaderCommand.FocusSearch)]
    [InlineData("Escape", ReaderCommand.ClearSearch)]
    [InlineData("Enter", ReaderCommand.OpenInBrowser)]
    [InlineData("o", ReaderCommand.OpenInBrowser)]
    [InlineData("Home", ReaderCommand.FirstArticle)]
    [InlineData("End", ReaderCommand.LastArticle)]
    [InlineData("A", ReaderCommand.MarkAllRead)]
    [InlineData("x", ReaderCommand.None)]
    public void Maps_keys_to_commands(string key, ReaderCommand expected)
    {
        Assert.Equal(expected, KeyboardMap.Resolve(key, shift: true, isSearchFocused: false));
    }

    [Fact]
    public void Shift_question_opens_the_shortcut_sheet()
    {
        Assert.Equal(ReaderCommand.ShowShortcuts, KeyboardMap.Resolve("?", shift: true, isSearchFocused: false));
    }

    [Fact]
    public void Typing_in_the_search_box_only_honors_escape()
    {
        Assert.Equal(ReaderCommand.None, KeyboardMap.Resolve("j", shift: false, isSearchFocused: true));
        Assert.Equal(ReaderCommand.None, KeyboardMap.Resolve("r", shift: false, isSearchFocused: true));
        Assert.Equal(ReaderCommand.None, KeyboardMap.Resolve("s", shift: false, isSearchFocused: true));
        Assert.Equal(ReaderCommand.ClearSearch, KeyboardMap.Resolve("Escape", shift: false, isSearchFocused: true));
    }

    [Fact]
    public void The_cheat_sheet_lists_every_documented_binding()
    {
        Assert.Contains(KeyboardMap.CheatSheet, entry => entry.Keys == "j");
        Assert.Contains(KeyboardMap.CheatSheet, entry => entry.Keys == "?");
        Assert.Contains(KeyboardMap.CheatSheet, entry => entry.Keys.Contains("Shift+A", StringComparison.Ordinal));
    }
}
