using System.Globalization;
using HackerNewsCrawler.Api.Tracking;
using Microsoft.Data.Sqlite;

namespace HackerNewsCrawler.Tests;

[Trait("Category", "Integration")]
public class SqliteUsageTrackerTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "HackerNewsCrawler.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(directory, "usage.db");

    [Fact]
    public void RecordUsage_PersistsBothFiltersAcrossInstancesWithUtcTimestamps()
    {
        var requestedAt = new DateTimeOffset(2026, 10, 23, 10, 30, 0, TimeSpan.FromHours(-5));
        new SqliteUsageTracker(DatabasePath).RecordUsage(FilterId.A, requestedAt, 12);
        new SqliteUsageTracker(DatabasePath).RecordUsage(FilterId.B, requestedAt, 0);

        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT RequestedAtUtc, FilterId, ResultCount FROM FilterUsage ORDER BY Id";
        using var reader = command.ExecuteReader();

        Assert.True(reader.Read());
        var timestamp = DateTimeOffset.Parse(reader.GetString(0), CultureInfo.InvariantCulture);
        Assert.Equal(requestedAt.ToUniversalTime(), timestamp);
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.Equal("A", reader.GetString(1));
        Assert.Equal(12, reader.GetInt32(2));
        Assert.True(reader.Read());
        Assert.Equal("B", reader.GetString(1));
        Assert.Equal(0, reader.GetInt32(2));
        Assert.False(reader.Read());
    }

    [Theory]
    [InlineData((FilterId)99, 1)]
    [InlineData(FilterId.A, -1)]
    public void RecordUsage_RejectsInvalidInputBeforeCreatingDatabase(FilterId filterId, int count)
    {
        var tracker = new SqliteUsageTracker(DatabasePath);

        Assert.Throws<ArgumentOutOfRangeException>(() => tracker.RecordUsage(filterId, DateTimeOffset.UtcNow, count));
        Assert.False(File.Exists(DatabasePath));
    }

    [Fact]
    public void RecordUsage_ReportsDatabaseOpenFailure()
    {
        Directory.CreateDirectory(DatabasePath);
        var tracker = new SqliteUsageTracker(DatabasePath);

        Assert.Throws<SqliteException>(() => tracker.RecordUsage(FilterId.A, DateTimeOffset.UtcNow, 1));
    }

    [Fact]
    public void RecordUsage_ReportsInsertFailureWithoutLosingExistingRecords()
    {
        var tracker = new SqliteUsageTracker(DatabasePath);
        tracker.RecordUsage(FilterId.A, DateTimeOffset.UtcNow, 3);
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER RejectUsage BEFORE INSERT ON FilterUsage
            BEGIN SELECT RAISE(ABORT, 'Storage failure'); END;
            """;
        command.ExecuteNonQuery();

        Assert.Throws<SqliteException>(() => tracker.RecordUsage(FilterId.B, DateTimeOffset.UtcNow, 5));

        command.CommandText = "SELECT COUNT(*) FROM FilterUsage";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    private SqliteConnection OpenDatabase()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false
        }.ToString());
        connection.Open();
        return connection;
    }

    [Fact]
    public void RecordUsages_RollsBackBothFiltersIfSecondInsertFails()
    {
        var tracker = new SqliteUsageTracker(DatabasePath);
        tracker.RecordUsage(FilterId.A, DateTimeOffset.UtcNow, 1);
        using var connection = OpenDatabase();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TRIGGER RejectB BEFORE INSERT ON FilterUsage WHEN NEW.FilterId = 'B'
            BEGIN SELECT RAISE(ABORT, 'Storage failure'); END;
            """;
        command.ExecuteNonQuery();

        Assert.Throws<SqliteException>(() => tracker.RecordUsages(
            new Dictionary<FilterId, int> { [FilterId.A] = 10, [FilterId.B] = 20 }, DateTimeOffset.UtcNow));

        command.CommandText = "SELECT COUNT(*) FROM FilterUsage";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}
