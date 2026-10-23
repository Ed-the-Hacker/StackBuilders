using HackerNewsCrawler.Api.Models;

namespace HackerNewsCrawler.Api.Scraping;

public sealed class HackerNewsClient(HttpClient httpClient, HackerNewsHtmlParser parser)
{
    public async Task<IReadOnlyList<HackerNewsEntry>> GetEntriesAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync(
                "https://news.ycombinator.com/", cancellationToken);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return parser.Parse(html);
        }
        catch (OperationCanceledException exception) when (
            !cancellationToken.IsCancellationRequested && exception.InnerException is TimeoutException)
        {
            throw new TimeoutException("The Hacker News request timed out.", exception);
        }
    }
}
