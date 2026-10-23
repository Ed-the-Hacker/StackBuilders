using HackerNewsCrawler.Api.Endpoints;
using HackerNewsCrawler.Api.Scraping;
using HackerNewsCrawler.Api.Tracking;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<HackerNewsHtmlParser>();
builder.Services.AddHttpClient<HackerNewsClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HackerNewsCrawler/1.0");
});
builder.Services.AddSingleton(new SqliteUsageTracker(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "usage.db")));

var app = builder.Build();
app.UseExceptionHandler();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/entries", EntriesEndpoint.HandleAsync);

app.Run();

public partial class Program;
