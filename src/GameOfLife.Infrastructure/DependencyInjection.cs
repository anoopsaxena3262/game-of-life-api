using GameOfLife.Application;
using GameOfLife.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure;

public static class DependencyInjection
{
    public const string SqliteProvider = "Sqlite";
    public const string PostgresProvider = "Postgres";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Persistence:Provider"] ?? SqliteProvider;
        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=data/game-of-life.db";

        if (string.Equals(provider, PostgresProvider, StringComparison.OrdinalIgnoreCase))
        {
            services.AddDbContext<GameOfLifeDbContext>(options => options.UseNpgsql(connectionString));
        }
        else
        {
            EnsureSqliteDirectory(connectionString);
            services.AddDbContext<GameOfLifeDbContext>(options => options.UseSqlite(connectionString));
        }

        services.AddScoped<IBoardRepository, BoardRepository>();
        services.AddScoped<IGenerationCache, GenerationCache>();
        services.AddHealthChecks()
            .AddDbContextCheck<GameOfLifeDbContext>(name: "database", tags: ["ready"]);

        return services;
    }

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameOfLifeDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    private static void EnsureSqliteDirectory(string connectionString)
    {
        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
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
