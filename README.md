# Ethang RSS

A local-first Windows desktop RSS reader. It replaces the web reader: same subscriptions, its own
SQLite database, no browser tab, no server, no account.

Everything runs on your machine. The app talks to the outside world only to fetch feeds.

## What it does

- Three-pane reader: folders and feeds on the left, article list in the middle, the article itself on the right.
- Keyboard first: `j`/`k` to move, `s` to star, `m` to mark unread, `/` to search, `?` for the full list.
- Fetches every feed in the background on a timer, six at a time, and records failures per feed instead of interrupting you.
- Searches article titles and bodies (SQLite full-text index).
- Imports the subscriptions you already have on Cloudflare, once, with one command.

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

## Import your existing subscriptions

The reader stores nothing about Cloudflare. You export the production database once and feed that
dump to the importer.

**1. Export the D1 database** (needs `wrangler` logged in, and the worker checkout):

```
npx --yes wrangler@4 --cwd <path-to>/apps/ethang-rss d1 export ethang-rss --remote --no-schema --output d1-export.sql
```

**2. Preview what will be imported:**

```
dotnet run --project src/EthangRss.Import -- --dump d1-export.sql --dry-run
```

**3. Import into the app database:**

```
dotnet run --project src/EthangRss.Import -- --dump d1-export.sql
```

Only subscribed feeds come across. Articles do not: the reader fetches them itself on first run, so
your library fills with current content rather than a stale copy. Running the import twice is safe
and adds nothing the second time.

## Where your data lives

```
%LOCALAPPDATA%\EthangRss\ethang-rss.db
```

Images the reader caches are under `%LOCALAPPDATA%\EthangRss\images`. Delete the whole folder to
start clean. Settings has an **Open database folder** button so you never have to type that path.

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

- **Refresh interval** — how often feeds are checked. Default 30 minutes.
- **Keep every article forever** — on by default. Turn it off and set a number of days to prune old
  articles; starred ones are always kept.
- **Filter out YouTube Shorts** — on by default. Shorts are clips, not articles.
- **Notifications** — off by default. When on, one batched notification per check.
- **Start with Windows** — off by default.

## Privacy

There is no login, no account, and no sync. The database is a plain SQLite file on your own disk,
protected by your Windows account like any other file. Nothing is sent anywhere except feed
requests to the sites you subscribed to and their images.

## Layout

```
src/EthangRss.Core        domain: database, parsing, fetching, polling, search, import
src/EthangRss.Desktop     Avalonia window, view models, rendering
src/EthangRss.Import      one-shot Cloudflare D1 subscription importer
tests/                    unit tests for everything above
```

## Tests

```
dotnet test EthangRss.slnx
```
