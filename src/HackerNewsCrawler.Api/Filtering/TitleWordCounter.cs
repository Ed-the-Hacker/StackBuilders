namespace HackerNewsCrawler.Api.Filtering;

public static class TitleWordCounter
{
    public static int CountWords(string title)
    {
        ArgumentNullException.ThrowIfNull(title);

        var tokens = title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        // A token containing only punctuation or symbols is not a word.
        return tokens.Count(token => token.Any(char.IsLetterOrDigit));
    }
}
