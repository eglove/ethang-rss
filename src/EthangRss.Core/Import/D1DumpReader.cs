using System.Text;
using System.Web;

namespace EthangRss.Core.Import;

/// <summary>
/// Reads the two tables the port needs out of a Cloudflare D1 SQL dump. The dump is INSERT
/// statements only and can be tens of megabytes, so it is streamed line by line and never held
/// whole. Values are matched to columns by name, never by position, because the dump was authored
/// by older migrations whose column order is not the current one.
/// </summary>
public sealed class D1DumpReader
{
    private static readonly char[] TrimChars = [' ', '\t', '\r', '\n'];
    private const char Quote = '\'';

    public D1Dump Read(string dumpPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dumpPath);

        var feeds = new List<D1Feed>();
        var feedIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var statement in Statements(dumpPath, "feeds"))
        {
            var id = statement.Value("id");
            var xmlAddress = statement.Value("xmlAddress");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(xmlAddress))
            {
                continue;
            }

            if (!feedIds.Add(id))
            {
                continue;
            }

            feeds.Add(new D1Feed(
                id,
                xmlAddress,
                Clean(statement.Value("title")),
                Clean(statement.Value("website")),
                Clean(statement.Value("iconUrl"))));
        }

        var subscribed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var statement in Statements(dumpPath, "subscriptions"))
        {
            var feedId = statement.Value("feedId");
            if (!string.IsNullOrWhiteSpace(feedId))
            {
                subscribed.Add(feedId);
            }
        }

        var kept = feeds.Where(feed => subscribed.Contains(feed.Id)).ToList();
        return new D1Dump(kept, feeds.Count - kept.Count, subscribed.Count);
    }

    private static IEnumerable<ParsedStatement> Statements(string dumpPath, string table)
    {
        using var stream = new StreamReader(dumpPath);
        string? line;
        while ((line = stream.ReadLine()) is not null)
        {
            if (!line.StartsWith("INSERT INTO", StringComparison.Ordinal))
            {
                continue;
            }

            var statement = TryParse(line);
            if (statement is not null && string.Equals(statement.Table, table, StringComparison.Ordinal))
            {
                yield return statement;
            }
        }
    }

    private static ParsedStatement? TryParse(string line)
    {
        var open = line.IndexOf('(', StringComparison.Ordinal);
        var close = open < 0 ? -1 : line.IndexOf(')', open + 1);
        if (open < 0 || close < 0)
        {
            return null;
        }

        var tableStart = line.IndexOf('\"', StringComparison.Ordinal) + 1;
        if (tableStart <= 0)
        {
            return null;
        }

        var tableEnd = line.IndexOf('\"', tableStart);
        if (tableEnd < 0)
        {
            return null;
        }

        var table = line[tableStart..tableEnd];

        var columns = line[(open + 1)..close]
            .Split(',')
            .Select(column => column.Trim(TrimChars).Trim('\"'))
            .ToList();

        var valuesKeyword = line.IndexOf("VALUES", close, StringComparison.Ordinal);
        if (valuesKeyword < 0)
        {
            return null;
        }

        var values = ParseValues(line[(valuesKeyword + 6)..]);
        if (values is null || values.Count != columns.Count)
        {
            return null;
        }

        var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < columns.Count; index++)
        {
            map[columns[index]] = values[index];
        }

        return new ParsedStatement(table, map);
    }

    private static List<string?>? ParseValues(string remainder)
    {
        var values = new List<string?>();
        var index = remainder.IndexOf('(', StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }

        index++;
        var current = new StringBuilder();
        var inString = false;

        while (index < remainder.Length)
        {
            var character = remainder[index];

            if (inString)
            {
                if (character != Quote)
                {
                    current.Append(character);
                    index++;
                    continue;
                }

                if (index + 1 < remainder.Length && remainder[index + 1] == Quote)
                {
                    current.Append(Quote);
                    index += 2;
                    continue;
                }

                inString = false;
                index++;
                continue;
            }

            if (character == Quote)
            {
                inString = true;
                index++;
                continue;
            }

            if (character == ',')
            {
                values.Add(Finish(current));
                index++;
                continue;
            }

            if (character == ')')
            {
                values.Add(Finish(current));
                return values;
            }

            if (character is not ' ' and not '\t')
            {
                current.Append(character);
            }

            index++;
        }

        return null;
    }

    private static string? Finish(StringBuilder builder)
    {
        var value = builder.ToString();
        builder.Clear();
        return value is "NULL" ? null : value;
    }

    /// <summary>
    /// Some feed metadata was stored exactly as it arrived in feed XML, so escaped HTML entities
    /// survive into the dump. Decoding them keeps ampersands in icon and website addresses intact.
    /// </summary>
    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : HttpUtility.HtmlDecode(value).Trim();

    private sealed record ParsedStatement(string Table, Dictionary<string, string?> Values)
    {
        public string? Value(string column) =>
            Values.TryGetValue(column, out var value) ? value : null;
    }
}
