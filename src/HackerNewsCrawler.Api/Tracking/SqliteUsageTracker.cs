using System.Globalization;
using Microsoft.Data.Sqlite;

namespace HackerNewsCrawler.Api.Tracking;

public sealed class SqliteUsageTracker
{
    private readonly string databasePath;
    private readonly string connectionString;

    public SqliteUsageTracker(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        this.databasePath = Path.GetFullPath(databasePath);
        connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = this.databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = 5
        }.ToString();
    }

    public void RecordUsage(FilterId filterId, DateTimeOffset requestedAt, int resultCount)
        => RecordUsages(new Dictionary<FilterId, int> { [filterId] = resultCount }, requestedAt);

    public void RecordUsages(IReadOnlyDictionary<FilterId, int> usages, DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(usages);
        foreach (var (filterId, resultCount) in usages)
        {
            if (!Enum.IsDefined(filterId))
                throw new ArgumentOutOfRangeException(nameof(usages));
            ArgumentOutOfRangeException.ThrowIfNegative(resultCount);
        }
        if (usages.Count == 0) return;

        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();

        using var schema = connection.CreateCommand();
        schema.Transaction = transaction;
        schema.CommandText = """
            CREATE TABLE IF NOT EXISTS FilterUsage (
                Id INTEGER PRIMARY KEY,
                RequestedAtUtc TEXT NOT NULL,
                FilterId TEXT NOT NULL CHECK (FilterId IN ('A', 'B')),
                ResultCount INTEGER NOT NULL CHECK (ResultCount >= 0)
            );
            """;
        schema.ExecuteNonQuery();

        foreach (var (filterId, resultCount) in usages)
        {
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
            INSERT INTO FilterUsage (RequestedAtUtc, FilterId, ResultCount)
            VALUES ($requestedAt, $filterId, $resultCount);
            """;
            insert.Parameters.AddWithValue("$requestedAt", requestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("$filterId", filterId.ToString());
            insert.Parameters.AddWithValue("$resultCount", resultCount);
            insert.ExecuteNonQuery();
        }
        transaction.Commit();
    }
}
