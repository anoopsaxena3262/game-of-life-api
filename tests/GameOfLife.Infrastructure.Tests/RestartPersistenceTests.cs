using GameOfLife.Application;
using GameOfLife.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Infrastructure.Tests;

/// <summary>
/// The service must survive a restart and keep board state. This builds the
/// infrastructure the way the application does, writes, tears it down along with the
/// connection pool, and reads back from a second container on the same file.
/// </summary>
public sealed class RestartPersistenceTests : IDisposable
{
    /// <summary>Far enough that the cache holds more than generation 0, which save writes itself.</summary>
    private const int CachedThrough = 4;

    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Board_and_cached_generations_survive_a_restart()
    {
        var uploaded = new Board(Guid.NewGuid(), 3, 3, "000111000", DateTimeOffset.UtcNow, null);
        var cached = new List<string> { uploaded.InitialState };

        await using (var first = await StartAsync())
        {
            await using var scope = first.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();
            await repository.SaveAsync(uploaded, CancellationToken.None);

            var state = uploaded.InitialState;
            for (var index = 1; index <= CachedThrough; index++)
            {
                state = LifeEngine.Step(state, uploaded.Width, uploaded.Height);
                await repository.SaveGenerationAsync(uploaded.Id, index, state, CancellationToken.None);
                cached.Add(state);
            }
        }

        // Nothing from the first container may still hold the file open.
        SqliteConnection.ClearAllPools();

        await using (var second = await StartAsync())
        {
            await using var scope = second.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();

            Assert.Equal(uploaded, await repository.FindByIdAsync(uploaded.Id, CancellationToken.None));
            Assert.Equal(CachedThrough, await repository.FindHighestCachedIndexAsync(uploaded.Id, CancellationToken.None));
            for (var index = 0; index <= CachedThrough; index++)
            {
                Assert.Equal(cached[index], await repository.FindGenerationAsync(uploaded.Id, index, CancellationToken.None));
            }
        }
    }

    private async Task<ServiceProvider> StartAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _database.ConnectionString,
            })
            .Build();

        var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
        await provider.InitializeDatabaseAsync();
        return provider;
    }
}
