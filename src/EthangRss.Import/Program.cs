using EthangRss.Core.Data;
using EthangRss.Core.Import;

var dump = Arguments.Value(args, "--dump");
var databasePath = Arguments.Value(args, "--database");
var dryRun = Arguments.Flag(args, "--dry-run");

if (args is ["-h"] or ["--help"] || string.IsNullOrWhiteSpace(dump))
{
    Console.WriteLine("""
        Ports Cloudflare RSS subscriptions into the local reader database.

        Usage:
          EthangRss.Import --dump <path-to-d1-export.sql> [--database <path>] [--dry-run]

        Produce the dump first:
          npx --yes wrangler@4 --cwd <ethang-rss> d1 export ethang-rss --remote --no-schema --output d1-export.sql
        """);
    return string.IsNullOrWhiteSpace(dump) && args.Length > 0 ? 1 : 0;
}

if (!File.Exists(dump))
{
    Console.Error.WriteLine($"The dump file was not found: {dump}");
    return 1;
}

var database = string.IsNullOrWhiteSpace(databasePath)
    ? new AppDatabase()
    : new AppDatabase(databasePath);

if (dryRun)
{
    var preview = new D1DumpReader().Read(dump);
    Console.WriteLine($"Subscribed feeds to import: {preview.SubscribedFeeds.Count}");
    Console.WriteLine($"Feeds without a subscription (skipped): {preview.SkippedUnsubscribed}");
    foreach (var feed in preview.SubscribedFeeds.Take(10))
    {
        Console.WriteLine($"  {feed.Title ?? feed.XmlAddress}");
    }

    return 0;
}

database.Migrate();
var result = new SubscriptionImporter(database).Import(dump);

Console.WriteLine($"Database: {database.DatabasePath}");
Console.WriteLine($"FeedsImported={result.FeedsImported}");
Console.WriteLine($"AlreadyPresent={result.AlreadyPresent}");
Console.WriteLine($"SkippedUnsubscribed={result.SkippedUnsubscribed}");
Console.WriteLine($"TotalSubscribed={result.Kept}");
return result.Kept > 0 ? 0 : 1;

internal static class Arguments
{
    public static string? Value(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    public static bool Flag(string[] args, string name) => Array.IndexOf(args, name) >= 0;
}
