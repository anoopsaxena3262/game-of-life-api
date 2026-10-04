using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace GameOfLife.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath;
    private readonly bool _ownsDatabase;

    /// <summary>Hosts the API on its own temporary database file, deleted on dispose.</summary>
    public ApiFactory()
        : this(Path.Combine(Path.GetTempPath(), $"game-of-life-{Guid.NewGuid():N}.db"), ownsDatabase: true)
    {
    }

    // xUnit requires a class fixture to have a single public constructor.
    private ApiFactory(string databasePath, bool ownsDatabase)
    {
        _databasePath = databasePath;
        _ownsDatabase = ownsDatabase;
    }

    /// <summary>Hosts the API on a caller's database file, which outlives this host.</summary>
    public static ApiFactory OnDatabase(string databasePath) => new(databasePath, ownsDatabase: false);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath};Default Timeout=5");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (_ownsDatabase)
        {
            DeleteDatabase(_databasePath);
        }
    }

    public static void DeleteDatabase(string path)
    {
        // Pooled connections keep the file open; release them before deleting.
        SqliteConnection.ClearAllPools();
        TryDelete(path);
        TryDelete(path + "-wal");
        TryDelete(path + "-shm");
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
