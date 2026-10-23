namespace HackerNewsCrawler.Api.Models;

public sealed record HackerNewsEntry(
    int Rank,
    string Title,
    int Points,
    int CommentCount);
