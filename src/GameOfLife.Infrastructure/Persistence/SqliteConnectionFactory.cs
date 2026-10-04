using Microsoft.Data.Sqlite;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>Opens connections to the configured SQLite file. Pooling is on by default in the driver.</summary>
public sealed class SqliteConnectionFactory(string connectionString)
{
    public string ConnectionString { get; } = connectionString;

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
