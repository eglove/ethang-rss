using EthangRss.Core.Data;

namespace EthangRss.Core;

/// <summary>
/// Small key/value store on top of the settings table. Everything here has a default, so a fresh
/// database behaves sensibly without an onboarding step being mandatory.
/// </summary>
public sealed class SettingsStore
{
    private readonly AppDatabase _database;

    public SettingsStore(AppDatabase database)
    {
        ArgumentNullException.ThrowIfNull(database);
        _database = database;
    }

    public string? Get(string key)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select Value from settings where Key = $key";
        command.Parameters.AddWithValue("$key", key);
        return command.ExecuteScalar() as string;
    }

    public void Set(string key, string value)
    {
        using var connection = _database.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            "insert into settings (Key, Value) values ($key, $value) " +
            "on conflict (Key) do update set Value = excluded.Value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }

    public int GetInt(string key, int fallback) =>
        int.TryParse(Get(key), out var value) ? value : fallback;

    public bool GetBool(string key, bool fallback) =>
        bool.TryParse(Get(key), out var value) ? value : fallback;

    public void SetBool(string key, bool value) => Set(key, value ? "true" : "false");

    public void SetInt(string key, int value) => Set(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
