using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HackerNewsCrawler.Api.Endpoints;
using HackerNewsCrawler.Api.Scraping;
using HackerNewsCrawler.Api.Tracking;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace HackerNewsCrawler.Tests;

[Trait("Category", "Integration")]
public class EntriesEndpointTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "HackerNewsCrawler.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "usage.db");
    private int requests;

    [Theory]
    [InlineData("all", 0)]
    [InlineData("a", 1)]
    [InlineData("b", 1)]
    [InlineData("both", 2)]
    public async Task GetEntries_ReturnsSelectedFiltersAndPersistsUsageWithOneDownload(string filter, int usageCount)
    {
        await using var app = CreateApplication();
        using var client = app.CreateClient();
        var started = DateTimeOffset.UtcNow;

        using var response = await client.GetAsync($"/api/entries?filter={filter}");
        response.EnsureSuccessStatusCode();
        var data = (await response.Content.ReadFromJsonAsync<EntriesResponse>())!;

        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(30, data.Entries.Count);
        Assert.Equal(Enumerable.Range(1, 30), data.Entries.Select(entry => entry.Rank));
        Assert.Equal(filter is "a" or "both", data.FilterA is not null);
        Assert.Equal(filter is "b" or "both", data.FilterB is not null);
        Assert.Equal(1, requests);
        if (filter == "both")
            Assert.Equal(30, data.FilterA!.Count + data.FilterB!.Count);

        if (usageCount == 0)
        {
            Assert.False(File.Exists(DatabasePath));
            return;
        }

        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RequestedAtUtc, FilterId, ResultCount FROM FilterUsage ORDER BY Id";
        using var reader = command.ExecuteReader();
        var savedFilters = new List<string>();
        while (reader.Read())
        {
            var timestamp = DateTimeOffset.Parse(reader.GetString(0));
            Assert.Equal(TimeSpan.Zero, timestamp.Offset);
            Assert.InRange(timestamp, started, DateTimeOffset.UtcNow);
            var id = reader.GetString(1);
            savedFilters.Add(id);
            Assert.Equal(id == "A" ? data.FilterA!.Count : data.FilterB!.Count, reader.GetInt32(2));
        }
        Assert.Equal(usageCount, savedFilters.Count);
        Assert.Equal(filter == "both" ? new[] { "A", "B" } : new[] { filter.ToUpperInvariant() }, savedFilters);
    }

    [Fact]
    public async Task GetEntries_DefaultsToAllAndRejectsInvalidFilterBeforeDownloading()
    {
        await using var app = CreateApplication();
        using var client = app.CreateClient();

        using var invalid = await client.GetAsync("/api/entries?filter=unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
        Assert.Equal(0, requests);

        var data = await client.GetFromJsonAsync<EntriesResponse>("/api/entries");
        Assert.Null(data!.FilterA);
        Assert.Null(data.FilterB);
        Assert.False(File.Exists(DatabasePath));
    }

    [Theory]
    [InlineData("http", 502)]
    [InlineData("network", 502)]
    [InlineData("html", 502)]
    [InlineData("timeout", 504)]
    [InlineData("storage", 503)]
    public async Task GetEntries_ReturnsSafeProblemDetailsOnFailure(string failure, int expectedStatus)
    {
        if (failure == "storage") Directory.CreateDirectory(DatabasePath);
        await using var app = CreateApplication((_, _) => failure switch
        {
            "http" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)),
            "network" => throw new HttpRequestException("PRIVATE diagnostic"),
            "timeout" => throw new TimeoutException("PRIVATE diagnostic"),
            "html" => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Invalid HTML") }),
            _ => Task.FromResult(Homepage())
        });
        using var client = app.CreateClient();

        using var response = await client.GetAsync("/api/entries?filter=both");

        Assert.Equal(expectedStatus, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PRIVATE diagnostic", json);
        using var problem = JsonDocument.Parse(json);
        Assert.Equal(expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(problem.RootElement.GetProperty("detail").GetString()));
        Assert.Equal(1, requests);
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public async Task GetEntries_CancellationDoesNotWriteUsage()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var app = CreateApplication(async (_, token) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, token);
            return Homepage();
        });
        using var client = app.CreateClient();
        using var cancellation = new CancellationTokenSource();
        var request = client.GetAsync("/api/entries?filter=both", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.False(File.Exists(DatabasePath));
    }

    private WebApplicationFactory<Program> CreateApplication(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>? send = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton(new SqliteUsageTracker(DatabasePath));
            services.AddHttpClient<HackerNewsClient>().ConfigurePrimaryHttpMessageHandler(() =>
                new Handler((request, token) =>
                {
                    requests++;
                    return send is null ? Task.FromResult(Homepage()) : send(request, token);
                }));
        }));

    private static HttpResponseMessage Homepage() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "homepage.html")))
    };

    private SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath, Pooling = false, Mode = SqliteOpenMode.ReadWrite
        }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
    }
}
