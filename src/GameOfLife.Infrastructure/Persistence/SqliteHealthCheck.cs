using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>Ready when the database file opens and answers a trivial query.</summary>
public sealed class SqliteHealthCheck(SqliteConnectionFactory connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connections.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("database is not reachable", ex);
        }
    }
}
