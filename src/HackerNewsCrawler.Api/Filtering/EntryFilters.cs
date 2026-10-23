using HackerNewsCrawler.Api.Models;

namespace HackerNewsCrawler.Api.Filtering;

public static class EntryFilters
{
    public static IReadOnlyList<HackerNewsEntry> ApplyFilterA(IEnumerable<HackerNewsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Where(entry => TitleWordCounter.CountWords(entry.Title) > 5)
            .OrderByDescending(entry => entry.CommentCount)
            .ThenBy(entry => entry.Rank)
            .ToArray();
    }

    public static IReadOnlyList<HackerNewsEntry> ApplyFilterB(IEnumerable<HackerNewsEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Where(entry => TitleWordCounter.CountWords(entry.Title) <= 5)
            .OrderByDescending(entry => entry.Points)
            .ThenBy(entry => entry.Rank)
            .ToArray();
    }
}
