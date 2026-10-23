using HackerNewsCrawler.Api.Scraping;
using HackerNewsCrawler.Api.Tracking;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<HackerNewsHtmlParser>();
builder.Services.AddHttpClient<HackerNewsClient>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("HackerNewsCrawler/1.0");
});
builder.Services.AddSingleton(new SqliteUsageTracker(
    Path.Combine(builder.Environment.ContentRootPath, "Data", "usage.db")));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
