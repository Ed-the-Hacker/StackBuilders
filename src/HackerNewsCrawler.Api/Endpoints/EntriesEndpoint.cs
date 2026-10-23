using HackerNewsCrawler.Api.Filtering;
using HackerNewsCrawler.Api.Models;
using HackerNewsCrawler.Api.Scraping;
using HackerNewsCrawler.Api.Tracking;
using Microsoft.Data.Sqlite;

namespace HackerNewsCrawler.Api.Endpoints;

public static class EntriesEndpoint
{
    public static async Task<IResult> HandleAsync(
        string? filter, HackerNewsClient client, SqliteUsageTracker tracker,
        ILoggerFactory loggerFactory, HttpResponse response, CancellationToken cancellationToken)
    {
        response.Headers.CacheControl = "no-store";
        var selection = (filter ?? "all").ToLowerInvariant();
        if (selection is not ("all" or "a" or "b" or "both"))
            return Results.Problem(statusCode: 400, title: "Invalid filter",
                detail: "Use all, a, b, or both.");

        var requestedAt = DateTimeOffset.UtcNow;
        try
        {
            var entries = await client.GetEntriesAsync(cancellationToken);
            var filterA = selection is "a" or "both" ? EntryFilters.ApplyFilterA(entries) : null;
            var filterB = selection is "b" or "both" ? EntryFilters.ApplyFilterB(entries) : null;
            var usage = new Dictionary<FilterId, int>();
            if (filterA is not null) usage.Add(FilterId.A, filterA.Count);
            if (filterB is not null) usage.Add(FilterId.B, filterB.Count);

            cancellationToken.ThrowIfCancellationRequested();
            if (usage.Count > 0) tracker.RecordUsages(usage, requestedAt);

            return Results.Ok(new EntriesResponse(entries, filterA, filterB));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Results.Problem(statusCode: 499, title: "Request canceled",
                detail: "The client canceled the request.");
        }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException
            or FormatException or SqliteException or IOException or UnauthorizedAccessException)
        {
            loggerFactory.CreateLogger("EntriesEndpoint").LogWarning(exception, "Could not complete the news request.");
            return exception switch
            {
                TimeoutException => Results.Problem(statusCode: 504, title: "Hacker News timed out",
                    detail: "Hacker News took too long to respond. Please try again."),
                HttpRequestException or FormatException => Results.Problem(statusCode: 502, title: "Hacker News unavailable",
                    detail: "Could not retrieve a valid page from Hacker News. Please try again."),
                _ => Results.Problem(statusCode: 503, title: "Usage storage unavailable",
                    detail: "Could not save filter usage. Please try again later.")
            };
        }
    }
}

public sealed record EntriesResponse(
    IReadOnlyList<HackerNewsEntry> Entries,
    IReadOnlyList<HackerNewsEntry>? FilterA,
    IReadOnlyList<HackerNewsEntry>? FilterB);
