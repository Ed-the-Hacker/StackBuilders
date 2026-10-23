using HackerNewsCrawler.Api.Models;
using HackerNewsCrawler.Api.Scraping;

namespace HackerNewsCrawler.Tests;

public class HackerNewsHtmlParserTests
{
    private readonly HackerNewsHtmlParser parser = new();

    private static string Fixture(string name = "entries.html") =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [Fact]
    public void Parse_ExtractsFieldsDecodesEntitiesAndPreservesDisplayedRanks()
    {
        var entries = parser.Parse(Fixture());

        Assert.Equal(3, entries.Count);
        Assert.Equal(new HackerNewsEntry(7, "C# & .NET: \"hello\" <world> é", 42, 12), entries[0]);
        Assert.Equal(new HackerNewsEntry(9, "A self-explained example", 1, 0), entries[1]);
        Assert.Equal(new HackerNewsEntry(15, "Hiring a developer", 0, 0), entries[2]);
    }

    [Fact]
    public void Parse_HandlesSingularCommentAndWhitespace()
    {
        var html = Fixture().Replace("12&nbsp;comments", " 1\n\tcomment ")
            .Replace("42 points", " 42\t points ")
            .Replace("athing submission", "extra  athing   submission");

        Assert.Equal(1, parser.Parse(html)[0].CommentCount);
        Assert.Equal(42, parser.Parse(html)[0].Points);
    }

    [Fact]
    public void Parse_MissingScoreDoesNotLoseComments()
    {
        var html = Fixture().Replace("<span class=\"score\" id=\"score_101\">42 points</span>", "");

        var entry = parser.Parse(html)[0];

        Assert.Equal(0, entry.Points);
        Assert.Equal(12, entry.CommentCount);
    }

    [Fact]
    public void Parse_MissingDiscussionLinkDoesNotUseAgeAsCommentCount()
    {
        var html = Fixture().Replace("<a href=\"item?id=101\">12&nbsp;comments</a>", "");

        Assert.Equal(0, parser.Parse(html)[0].CommentCount);
        Assert.Equal(42, parser.Parse(html)[0].Points);
    }

    [Fact]
    public void Parse_ReadsThirtyEntriesFromCapturedHomepage()
    {
        var entries = parser.Parse(Fixture("homepage.html"));

        Assert.Equal(30, entries.Count);
        Assert.Equal(Enumerable.Range(1, 30), entries.Select(entry => entry.Rank));
        Assert.Equal(new HackerNewsEntry(1, "Kimi K3.1 Pro and K3.1 Pro Highspeed", 607, 368), entries[0]);
        Assert.Equal("Total Historian", entries[^1].Title);
    }

    [Fact]
    public void Parse_IgnoresEntriesAfterTheFirstThirty()
    {
        var html = Fixture("homepage.html").Replace("</body>", "<table><tr class='athing'></tr></table></body>");

        Assert.Equal(30, parser.Parse(html).Count);
    }

    [Theory]
    [InlineData("7.", "bad")]
    [InlineData("7.", "0.")]
    [InlineData("7.", "-1.")]
    [InlineData("7.", "2147483648.")]
    [InlineData("9.", "7.")]
    [InlineData("42 points", "many points")]
    [InlineData("42 points", "-1 points")]
    [InlineData("42 points", "2147483648 points")]
    [InlineData("42 points", "42 votes")]
    [InlineData("12&nbsp;comments", "many comments")]
    [InlineData("12&nbsp;comments", "-1 comments")]
    [InlineData("12&nbsp;comments", "12 replies")]
    [InlineData("12&nbsp;comments", "")]
    [InlineData("class=\"rank\"", "class=\"not-rank\"")]
    [InlineData("class=\"titleline\"", "class=\"not-titleline\"")]
    [InlineData("class=\"subtext\"", "class=\"not-subtext\"")]
    [InlineData("id=\"101\"", "id=\"102\"")]
    [InlineData("id=\"101\"", "id=\"\"")]
    [InlineData("score_101", "score_999")]
    [InlineData("item?id=101", "item?id=999")]
    public void Parse_RejectsInvalidFieldsInsteadOfReturningPartialResults(string original, string replacement)
    {
        Assert.Throws<FormatException>(() => parser.Parse(Fixture().Replace(original, replacement)));
    }

    [Fact]
    public void Parse_RejectsEmptyTitle()
    {
        var html = Fixture().Replace("A self-explained example", " &nbsp; ");

        Assert.Throws<FormatException>(() => parser.Parse(html));
    }

    [Fact]
    public void Parse_DoesNotBorrowMetadataFromTheNextEntry()
    {
        var html = Fixture().Replace("<tr><td class=\"subtext\">", "<tr class='spacer'><td></td></tr><tr><td class=\"subtext\">");

        Assert.Throws<FormatException>(() => parser.Parse(html));
    }

    [Fact]
    public void Parse_RejectsDuplicateDiscussionLinks()
    {
        var link = "<a href=\"item?id=101\">12&nbsp;comments</a>";

        Assert.Throws<FormatException>(() => parser.Parse(Fixture().Replace(link, link + link)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>Service unavailable</body></html>")]
    [InlineData("<table><tr class='not-athing'><td>Unexpected page</td></tr></table>")]
    public void Parse_RejectsUnrecognizedPages(string html)
    {
        Assert.Throws<FormatException>(() => parser.Parse(html));
    }

    [Fact]
    public void Parse_RejectsNullHtml()
    {
        Assert.Throws<ArgumentNullException>(() => parser.Parse(null!));
    }
}
