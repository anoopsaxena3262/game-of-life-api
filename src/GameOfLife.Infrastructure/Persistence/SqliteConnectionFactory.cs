using Microsoft.Data.Sqlite;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>Opens connections to the configured SQLite file. Pooling is on by default in the driver.</summary>
public sealed class SqliteConnectionFactory
{
    public SqliteConnectionFactory(string connectionString)
    {
        // SQLite does not check foreign keys unless each connection turns them on.
        // The driver runs PRAGMA foreign_keys=ON on open when this is set.
        var builder = new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = true };
        ConnectionString = builder.ToString();
        DataSource = builder.DataSource;
    }

    public string ConnectionString { get; }

    /// <summary>The database file path as configured. Relative paths resolve against the working directory.</summary>
    public string DataSource { get; }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
