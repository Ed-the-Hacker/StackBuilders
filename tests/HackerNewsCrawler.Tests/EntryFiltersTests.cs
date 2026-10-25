using HackerNewsCrawler.Api.Filtering;
using HackerNewsCrawler.Api.Models;

namespace HackerNewsCrawler.Tests;

[Trait("Category", "Unit")]
public class EntryFiltersTests
{
    private static HackerNewsEntry[] Entries() =>
    [
        new(8, "one two three four five six", 900, 10, "https://example.com/story"),
        new(5, "one two three four five six seven", 1, 30, "https://example.com/story"),
        new(2, "one two three four five six", 2, 30, "https://example.com/story"),
        new(9, "one two three four five", 10, 900, "https://example.com/story"),
        new(6, "This is - a self-explained example", 50, 1, "https://example.com/story"),
        new(1, "Short title", 50, 2, "https://example.com/story")
    ];

    [Fact]
    public void FilterA_SelectsLongTitlesAndOrdersByCommentsThenRank()
    {
        var result = EntryFilters.ApplyFilterA(Entries());

        Assert.Equal(new[] { 2, 5, 8 }, result.Select(entry => entry.Rank));
    }

    [Fact]
    public void FilterB_SelectsShortTitlesAndOrdersByPointsThenRank()
    {
        var result = EntryFilters.ApplyFilterB(Entries());

        Assert.Equal(new[] { 1, 6, 9 }, result.Select(entry => entry.Rank));
    }

    [Fact]
    public void BothFilters_UseOriginalCollectionWithoutChangingIt()
    {
        var entries = Entries();
        var original = entries.ToArray();

        var longTitles = EntryFilters.ApplyFilterA(entries);
        var shortTitles = EntryFilters.ApplyFilterB(entries);

        Assert.Equal(original, entries);
        Assert.Equal(original.OrderBy(entry => entry.Rank),
            longTitles.Concat(shortTitles).OrderBy(entry => entry.Rank));
    }

    [Fact]
    public void Filters_ReturnEmptyResultsForEmptyInput()
    {
        Assert.Empty(EntryFilters.ApplyFilterA([]));
        Assert.Empty(EntryFilters.ApplyFilterB([]));
    }

    [Fact]
    public void Filters_ReturnEmptyResultsWhenNoTitleMatches()
    {
        Assert.Empty(EntryFilters.ApplyFilterA([new(1, "Short", 0, 0, "https://example.com/story")]));
        Assert.Empty(EntryFilters.ApplyFilterB([new(1, "one two three four five six", 0, 0, "https://example.com/story")]));
    }

    [Fact]
    public void FilterB_IncludesEmptyTitlesAndZeroScores()
    {
        var entries = new[] { new HackerNewsEntry(2, "", 0, 0, "https://example.com/story"), new HackerNewsEntry(1, "-", 0, 0, "https://example.com/story") };

        Assert.Equal(new[] { 1, 2 }, EntryFilters.ApplyFilterB(entries).Select(entry => entry.Rank));
    }

    [Fact]
    public void Filters_RejectNullCollections()
    {
        Assert.Throws<ArgumentNullException>(() => EntryFilters.ApplyFilterA(null!));
        Assert.Throws<ArgumentNullException>(() => EntryFilters.ApplyFilterB(null!));
    }
}
