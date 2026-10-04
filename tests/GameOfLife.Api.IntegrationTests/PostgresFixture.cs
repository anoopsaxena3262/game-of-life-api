using Testcontainers.PostgreSql;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// Opt-in Postgres host. Not used by the default suite, so <c>dotnet test</c> does not need Docker.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder("postgres:17").Build();

    public Task InitializeAsync() => Container.StartAsync();

    public async Task DisposeAsync() => await Container.DisposeAsync();
}
