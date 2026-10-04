using GameOfLife.Application;
using GameOfLife.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure;

public static class DependencyInjection
{
    public const string DefaultConnectionString = "Data Source=data/game-of-life.db;Default Timeout=5";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default") ?? DefaultConnectionString;

        services.AddSingleton(new SqliteConnectionFactory(connectionString));
        services.AddSingleton<SchemaInitializer>();
        services.AddScoped<IBoardRepository, SqliteBoardRepository>();

        return services;
    }

    /// <summary>
    /// Creates the database directory and tables. Call once after the host is built and
    /// before it starts listening, so no request reaches a missing table.
    /// </summary>
    public static Task InitializeDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default) =>
        services.GetRequiredService<SchemaInitializer>().InitializeAsync(cancellationToken);
}
