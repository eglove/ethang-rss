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
only when you press `m`, click Mark read, or press `Shift+A` to clear the whole view you are looking
at - a folder, a single feed, or the results of a search.

Unsubscribing is explicit too. Select a feed, then press `Delete` or click Unsubscribe above the
article list. The app asks first and tells you how many stored articles the feed is holding, because
those are deleted with it. Folders have no Unsubscribe button: a folder is a view, not a
subscription.

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

Press Add feed and paste a feed address (https://example.com/feed.xml), just a website
(https://example.com), or a YouTube channel (https://www.youtube.com/@oompaville). For a website the
app reads the page's own feed declaration and falls back to the usual /feed, /rss, and /atom.xml
paths. Nothing is stored until the feed has actually been fetched and parsed, so a typo never
leaves a broken row behind.

## YouTube

YouTube stopped serving the per-channel Atom feed: youtube.com/feeds/videos.xml answers 404 for
every channel, not just some. The reader therefore fetches a channel's own videos page and reads the
entries out of it. A channel is stored as `youtube://channel/<id>`, so pasting a handle address or a
channel-id address adds the same subscription once.

Subscriptions added before this change are repointed automatically the next time the app starts and
keep the articles they already have.

The trade is that this is a page scraper: YouTube can change the page's shape at any time. When it
does, the affected feeds record a fetch error you can see in the feed list rather than quietly going
empty.

## Where your data lives

```
%LOCALAPPDATA%\EthangRss\ethang-rss.db
```

Images the reader caches are under `%LOCALAPPDATA%\EthangRss\images`. Delete the whole folder to start
clean. Settings has an Open database folder button so you never have to type that path.

If anything goes wrong, the app writes the details to `%LOCALAPPDATA%\EthangRss\crash.log` and keeps
running rather than closing.

## Keyboard

| Key | Action |
| --- | --- |
| `j` | next article |
| `k` | previous article |
| `Home` / `End` | first / last article |
| `Enter` or `o` | open the article in your browser |
| `s` | star or unstar |
| `m` | mark read or unread |
| `Shift+A` | mark everything in the current view read (folder, feed, or search) |
| `Delete` | unsubscribe from the selected feed |
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
