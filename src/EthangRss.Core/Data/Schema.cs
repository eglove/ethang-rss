namespace EthangRss.Core.Data;

/// <summary>
/// The reader database schema, as an ordered list of idempotent statements.
/// Every statement is safe to re-run, so migration can happen on every start.
/// </summary>
internal static class Schema
{
    public static readonly string[] Statements =
    [
        "CREATE TABLE IF NOT EXISTS feeds (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "XmlAddress TEXT NOT NULL UNIQUE, " +
            "Title TEXT NOT NULL, " +
            "Website TEXT, " +
            "IconUrl TEXT, " +
            "LastFetchedAt TEXT, " +
            "FetchIntervalMinutes INTEGER, " +
            "LastError TEXT, " +
            "LastErrorAt TEXT" +
            ")",

        "CREATE TABLE IF NOT EXISTS articles (" +
            "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
            "FeedId INTEGER NOT NULL REFERENCES feeds(Id) ON DELETE CASCADE, " +
            "Guid TEXT NOT NULL, " +
            "Title TEXT NOT NULL, " +
            "Link TEXT NOT NULL, " +
            "Content TEXT, " +
            "PublishedAt TEXT, " +
            "FetchedAt TEXT NOT NULL, " +
            "IsRead INTEGER NOT NULL DEFAULT 0, " +
            "IsStarred INTEGER NOT NULL DEFAULT 0, " +
            "UNIQUE (FeedId, Guid)" +
            ")",

        "CREATE INDEX IF NOT EXISTS IX_articles_FeedId ON articles(FeedId)",
        "CREATE INDEX IF NOT EXISTS IX_articles_IsRead ON articles(IsRead)",
        "CREATE INDEX IF NOT EXISTS IX_articles_PublishedAt ON articles(PublishedAt DESC)",

        "CREATE TABLE IF NOT EXISTS settings (Key TEXT PRIMARY KEY, Value TEXT)",

        "CREATE VIRTUAL TABLE IF NOT EXISTS articles_fts USING fts5(Title, Content, tokenize = 'unicode61 remove_diacritics 2')",

        "CREATE TRIGGER IF NOT EXISTS articles_fts_after_insert AFTER INSERT ON articles BEGIN " +
            "INSERT INTO articles_fts(rowid, Title, Content) VALUES (new.Id, new.Title, new.Content); END",

        "CREATE TRIGGER IF NOT EXISTS articles_fts_after_delete AFTER DELETE ON articles BEGIN " +
            "DELETE FROM articles_fts WHERE rowid = old.Id; END",

        "CREATE TRIGGER IF NOT EXISTS articles_fts_after_update AFTER UPDATE ON articles BEGIN " +
            "DELETE FROM articles_fts WHERE rowid = old.Id; " +
            "INSERT INTO articles_fts(rowid, Title, Content) VALUES (new.Id, new.Title, new.Content); END",
    ];
}
