using GameOfLife.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameOfLife.Infrastructure.Tests;

/// <summary>
/// A database file in its own temporary directory. A file, not :memory:, because the
/// file is what the durability requirement is about.
/// </summary>
internal sealed class TempDatabase : IDisposable
{
    public TempDatabase(string relativePath = "boards.db")
    {
        Directory = Path.Combine(Path.GetTempPath(), $"game-of-life-tests-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(Directory);
        FilePath = Path.Combine(Directory, relativePath);
    }

    public string Directory { get; }

    public string FilePath { get; }

    public string ConnectionString => $"Data Source={FilePath};Default Timeout=5";

    public SqliteConnectionFactory Connections() => new(ConnectionString);

    /// <summary>Runs the schema initializer and returns a repository over the same file.</summary>
    public async Task<SqliteBoardRepository> InitializedRepositoryAsync()
    {
        var connections = Connections();
        await new SchemaInitializer(connections, NullLogger<SchemaInitializer>.Instance)
            .InitializeAsync(CancellationToken.None);
        return new SqliteBoardRepository(connections, NullLogger<SqliteBoardRepository>.Instance);
    }

    public void Dispose()
    {
        // Pooled connections keep the file open; release them before deleting.
        SqliteConnection.ClearAllPools();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
