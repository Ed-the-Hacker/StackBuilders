using HackerNewsCrawler.Api.Filtering;

namespace HackerNewsCrawler.Tests;

[Trait("Category", "Unit")]
public class TitleWordCounterTests
{
    [Theory]
    [InlineData("This is - a self-explained example", 5)]
    [InlineData("one two three four five", 5)]
    [InlineData("one two three four five six", 6)]
    [InlineData("self-explained", 1)]
    [InlineData("A state-of-the-art compiler", 3)]
    [InlineData("C# & .NET: a practical guide", 5)]
    [InlineData("Hello, world!", 2)]
    [InlineData("hello,world", 1)]
    [InlineData("(hello) [world]", 2)]
    [InlineData("- + & / ... — !!!", 0)]
    [InlineData("Build 🚀 better tools", 3)]
    [InlineData("2026 release 2.0", 3)]
    [InlineData("Árboles y programación", 3)]
    [InlineData("你好 世界", 2)]
    [InlineData("  one   two  ", 2)]
    [InlineData("one\ttwo\r\nthree", 3)]
    [InlineData("one\u00A0two\u2003three", 3)]
    [InlineData("", 0)]
    [InlineData(" \t\r\n ", 0)]
    public void CountWords_ReturnsExpectedCount(string title, int expectedCount)
    {
        var count = TitleWordCounter.CountWords(title);

        Assert.Equal(expectedCount, count);
    }

    [Fact]
    public void CountWords_ThrowsWhenTitleIsNull()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => TitleWordCounter.CountWords(null!));

        Assert.Equal("title", exception.ParamName);
    }
}
