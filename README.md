# Ethang RSS

A local-first Windows desktop RSS reader. It has its own SQLite database, no browser tab, no server,
and no account. Everything runs on your machine; the app talks to the network only to fetch feeds.

## What it does

- Three-pane reader: folders and feeds on the left, article list in the middle, the article itself on the right.
- Keyboard first: `j`/`k` to move, `s` to star, `m` to mark read, `/` to search, `?` for the full list.
- Fetches every feed in the background on a timer, six at a time, and records failures per feed instead of interrupting you.
- Searches article titles and bodies (SQLite full-text index).
- Plays video articles inline, including YouTube, instead of showing a headline with no body.

Reading is explicit: moving through the list never changes what you have read. Marking read happens
only when you press `m`, click Mark read, or press `Shift+A` to clear a whole folder.

## Requirements

- Windows 10 or later, 64-bit.
- Either the published `EthangRss.exe` (nothing to install), or the .NET 10 SDK to build it.

## Run it

```
dotnet run --project src/EthangRss.Desktop
```

## Build the single-file executable

```
dotnet publish src/EthangRss.Desktop -c Release -r win-x64 --self-contained -o publish
```

That produces exactly one file, `publish/EthangRss.exe`, about 50 MB, with the runtime bundled in.
Copy it anywhere and double-click it.

## Add feeds

Press Add feed and paste either a feed address (https://example.com/feed.xml) or just a website
(https://example.com). For a website the app reads the page's own feed declaration and falls back to
the usual /feed, /rss, and /atom.xml paths. Nothing is stored until the feed has actually been
fetched and parsed, so a typo never leaves a broken row behind.

## Where your data lives

```
%LOCALAPPDATA%\EthangRss\ethang-rss.db
```

Images the reader caches are under `%LOCALAPPDATA%\EthangRss\images`. Delete the whole folder to start
clean. Settings has an Open database folder button so you never have to type that path.

## Keyboard

| Key | Action |
| --- | --- |
| `j` | next article |
| `k` | previous article |
| `Home` / `End` | first / last article |
| `Enter` or `o` | open the article in your browser |
| `s` | star or unstar |
| `m` | mark read or unread |
| `Shift+A` | mark everything in the current folder read |
| `u` | jump to Unread |
| `b` | jump to Starred |
| `/` | focus search |
| `Esc` | clear search |
| `r` | refresh feeds now |
| `?` | show the shortcut list |

## Settings

- Refresh interval: how often feeds are checked. Default 30 minutes.
- Keep every article forever: on by default. Turn it off and set a number of days to prune old
  articles; starred ones are always kept.
- Filter out YouTube Shorts: on by default. Shorts are clips, not articles.
- Notifications: off by default. When on, one batched notification per check.
- Start with Windows: off by default.

## Privacy

There is no login, no account, and no sync. The database is a plain SQLite file on your own disk,
protected by your Windows account like any other file. Nothing is sent anywhere except feed requests
to the sites you subscribed to, their images, and the video player when you open a video article.

## Layout

```
src/EthangRss.Core        domain: database, parsing, fetching, polling, search, content
src/EthangRss.Desktop     Avalonia window, view models, rendering, tray
tests/                    unit tests for everything above
```

## Tests

```
dotnet test EthangRss.slnx
```
