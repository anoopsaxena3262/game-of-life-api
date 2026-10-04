using GameOfLife.Application;
using GameOfLife.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString = "Data Source=data/game-of-life.db";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? DefaultConnectionString;

        EnsureSqliteDirectory(connectionString);

        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IGenerationCache, GenerationCache>();
        services.AddHealthChecks()
            .AddCheck<SqliteHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    /// <summary>SQLite creates the database file, but not a missing parent directory.</summary>
    private static void EnsureSqliteDirectory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.DataSource) || builder.DataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(builder.DataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
