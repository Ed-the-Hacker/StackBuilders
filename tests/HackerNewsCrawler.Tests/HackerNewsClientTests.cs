using System.Net;
using HackerNewsCrawler.Api.Filtering;
using HackerNewsCrawler.Api.Scraping;

namespace HackerNewsCrawler.Tests;

[Trait("Category", "Unit")]
public class HackerNewsClientTests
{
    [Fact]
    public async Task GetEntriesAsync_DownloadsOnceAndProvidesEntriesForBothFilters()
    {
        var calls = 0;
        using var httpClient = new HttpClient(new StubHandler((request, _) =>
        {
            calls++;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://news.ycombinator.com/", request.RequestUri?.AbsoluteUri);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(File.ReadAllText(
                    Path.Combine(AppContext.BaseDirectory, "Fixtures", "homepage.html")))
            });
        }));
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        var entries = await client.GetEntriesAsync();
        var longTitles = EntryFilters.ApplyFilterA(entries);
        var shortTitles = EntryFilters.ApplyFilterB(entries);

        Assert.Equal(30, entries.Count);
        Assert.NotEmpty(longTitles);
        Assert.NotEmpty(shortTitles);
        Assert.Equal(30, longTitles.Count + shortTitles.Count);
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task GetEntriesAsync_ReportsHttpErrorsWithoutRetrying(HttpStatusCode status)
    {
        var calls = 0;
        using var httpClient = new HttpClient(new StubHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(new HttpResponseMessage(status));
        }));
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => client.GetEntriesAsync());

        Assert.Equal(status, exception.StatusCode);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetEntriesAsync_PropagatesNetworkFailure()
    {
        using var httpClient = new HttpClient(new StubHandler((_, _) =>
            throw new HttpRequestException("Connection failed.")));
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetEntriesAsync());
    }

    [Fact]
    public async Task GetEntriesAsync_ReportsInvalidHtml()
    {
        using var httpClient = new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("<html>Unexpected response</html>")
            })));
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        await Assert.ThrowsAsync<FormatException>(() => client.GetEntriesAsync());
    }

    [Fact]
    public async Task GetEntriesAsync_ReportsTimeout()
    {
        using var httpClient = new HttpClient(new StubHandler(WaitForCancellation))
        {
            Timeout = TimeSpan.FromMilliseconds(100)
        };
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        await Assert.ThrowsAsync<TimeoutException>(() => client.GetEntriesAsync());
    }

    [Fact]
    public async Task GetEntriesAsync_PreservesCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var httpClient = new HttpClient(new StubHandler((request, token) =>
        {
            started.SetResult();
            return WaitForCancellation(request, token);
        }));
        var client = new HackerNewsClient(httpClient, new HackerNewsHtmlParser());

        var request = client.GetEntriesAsync(cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
    }

    private static async Task<HttpResponseMessage> WaitForCancellation(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The request should have been canceled.");
    }

    private sealed class StubHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
