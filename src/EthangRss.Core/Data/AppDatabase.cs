using Microsoft.Data.Sqlite;

namespace EthangRss.Core.Data;

/// <summary>
/// Owns the reader's SQLite database: where it lives, how connections are configured,
/// and the migration that brings a fresh file up to the current schema.
/// </summary>
public sealed class AppDatabase
{
    private const string FileName = "ethang-rss.db";

    public AppDatabase()
        : this(GetDefaultPath())
    {
    }

    public AppDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        DatabasePath = databasePath;
    }

    public string DatabasePath { get; }

    private string ConnectionString => new SqliteConnectionStringBuilder
    {
        DataSource = DatabasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
    }.ToString();

    public static string GetDefaultPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "EthangRss", FileName);
    }

    /// <summary>
    /// Opens a configured connection. The caller owns disposal. Write-ahead logging and
    /// foreign-key enforcement are per-connection, so both are applied here.
    /// </summary>
    public SqliteConnection Open()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    /// <summary>
    /// Creates every table, index, trigger, and the full-text index if they are missing.
    /// Safe to call on every start.
    /// </summary>
    public void Migrate()
    {
        using var connection = Open();
        foreach (var statement in Schema.Statements)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = statement;
                command.ExecuteNonQuery();
            }
            catch (SqliteException exception) when (IsAlreadyApplied(exception))
            {
                // Adding a column is the one statement SQLite cannot write idempotently, so a
                // database that already has it is the expected outcome on every start after the first.
            }
        }
    }

    private static bool IsAlreadyApplied(SqliteException exception) =>
        exception.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase);
}
