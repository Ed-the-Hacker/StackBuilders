using System.Globalization;
using HackerNewsCrawler.Api.Models;
using HtmlAgilityPack;

namespace HackerNewsCrawler.Api.Scraping;

public sealed class HackerNewsHtmlParser
{
    public IReadOnlyList<HackerNewsEntry> Parse(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var document = new HtmlDocument();
        document.LoadHtml(html);
        var rows = document.DocumentNode.SelectNodes($"//tr[{HasClass("athing")}]");
        if (rows is null || rows.Count == 0)
            throw new FormatException("The page does not contain recognizable Hacker News entries.");

        var entries = new List<HackerNewsEntry>();
        var ranks = new HashSet<int>();
        var ids = new HashSet<string>();

        foreach (var row in rows.Take(30))
        {
            var id = row.GetAttributeValue("id", "");
            if (string.IsNullOrWhiteSpace(id) || !ids.Add(id))
                throw new FormatException("An entry has a missing or duplicate identifier.");

            var rankText = Text(row.SelectSingleNode($".//span[{HasClass("rank")}]"));
            if (!rankText.EndsWith('.'))
                throw new FormatException($"Entry {id} has an invalid rank.");
            var rank = ParseNumber(rankText[..^1], "rank");
            if (rank == 0 || !ranks.Add(rank))
                throw new FormatException($"Entry {id} has a zero or duplicate rank.");

            var title = Text(row.SelectSingleNode($".//span[{HasClass("titleline")}]/a[1]"));
            if (string.IsNullOrWhiteSpace(title))
                throw new FormatException($"Entry {id} has no title.");

            // Only the immediately following row belongs to this entry.
            var metadataRow = row.SelectSingleNode("following-sibling::tr[1]");
            var metadata = metadataRow?.SelectSingleNode($"./td[{HasClass("subtext")}]");
            if (metadata is null)
                throw new FormatException($"Entry {id} has no metadata row.");

            var score = metadata.SelectSingleNode($".//span[{HasClass("score")}]");
            var scoreId = score?.GetAttributeValue("id", "");
            if (!string.IsNullOrEmpty(scoreId) && scoreId != $"score_{id}")
                throw new FormatException($"Entry {id} has mismatched score metadata.");
            // Job posts and other entries may omit points or the discussion link.
            var points = score is null ? 0 : ParseCount(Text(score), "point", "points");

            var discussionLinks = metadata.SelectNodes(".//a")?
                .Where(link => link.GetAttributeValue("href", "").StartsWith("item?id=", StringComparison.Ordinal))
                .Where(link => link.SelectSingleNode($"ancestor::*[{HasClass("age")}]") is null)
                .ToArray() ?? [];
            if (discussionLinks.Length > 1)
                throw new FormatException($"Entry {id} has multiple discussion links.");

            var comments = 0;
            if (discussionLinks.Length == 1)
            {
                if (HtmlEntity.DeEntitize(discussionLinks[0].GetAttributeValue("href", "")) != $"item?id={id}")
                    throw new FormatException($"Entry {id} has a mismatched discussion link.");

                var text = Text(discussionLinks[0]);
                comments = text == "discuss" ? 0 : ParseCount(text, "comment", "comments");
            }

            entries.Add(new HackerNewsEntry(rank, title, points, comments));
        }

        return entries;
    }

    private static string HasClass(string className) =>
        $"contains(concat(' ', normalize-space(@class), ' '), ' {className} ')";

    private static string Text(HtmlNode? node) =>
        string.Join(" ", HtmlEntity.DeEntitize(node?.InnerText ?? "")
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    private static int ParseCount(string text, string singular, string plural)
    {
        var parts = text.Split(' ');
        if (parts.Length != 2 || (parts[1] != singular && parts[1] != plural))
            throw new FormatException($"Invalid {plural} value: '{text}'.");

        return ParseNumber(parts[0], plural);
    }

    private static int ParseNumber(string text, string field)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"Invalid {field} value: '{text}'.");

        return value;
    }
}
