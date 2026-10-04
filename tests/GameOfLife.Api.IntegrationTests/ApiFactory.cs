using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GameOfLife.Api.IntegrationTests;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"game-of-life-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={_databasePath}");
        builder.UseSetting("OpenTelemetry:ConsoleExporter", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDelete(_databasePath);
        TryDelete(_databasePath + "-wal");
        TryDelete(_databasePath + "-shm");
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
