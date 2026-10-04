using GameOfLife.Application;
using GameOfLife.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace GameOfLife.Api.IntegrationTests;

/// <summary>
/// The direct test of the durability requirement: the service survives a restart and
/// keeps board state. A second host, started the way the application starts, reads what
/// the first host committed. A failure here is lifecycle or configuration; the
/// repository tests already prove the SQL.
/// </summary>
public sealed class RestartPersistenceTests : IDisposable
{
    /// <summary>Far enough that the cache holds more than generation 0, which save writes itself.</summary>
    private const int CachedThrough = 4;

    private readonly string _databasePath =
        Path.Combine(Path.GetTempPath(), $"game-of-life-restart-{Guid.NewGuid():N}.db");

    public void Dispose() => ApiFactory.DeleteDatabase(_databasePath);

    [Fact]
    public async Task Board_and_cached_generations_survive_an_application_restart()
    {
        Guid id;
        Board uploaded;
        var cached = new List<string>();

        await using (var first = ApiFactory.OnDatabase(_databasePath))
        {
            await using var scope = first.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BoardService>();
            id = await service.CreateAsync(3, 3, Blinker(), CancellationToken.None);
            // Reading generation 4 writes rows 1 through 4. Generation 0 was written on save.
            await service.GenerationAtAsync(id, CachedThrough, CancellationToken.None);

            var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();
            uploaded = (await repository.FindByIdAsync(id, CancellationToken.None))!;
            for (var index = 0; index <= CachedThrough; index++)
            {
                cached.Add((await repository.FindGenerationAsync(id, index, CancellationToken.None))!);
            }
        }

        // Nothing from the first host may still hold the file open.
        SqliteConnection.ClearAllPools();

        await using (var second = ApiFactory.OnDatabase(_databasePath))
        {
            await using var scope = second.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BoardService>();
            var repository = scope.ServiceProvider.GetRequiredService<IBoardRepository>();

            // Read the stored rows. GenerationAtAsync would recompute a missing cache and hide the loss.
            Assert.Equal(uploaded, await service.GetAsync(id, CancellationToken.None));
            Assert.Equal(CachedThrough, await repository.FindHighestCachedIndexAsync(id, CancellationToken.None));
            for (var index = 0; index <= CachedThrough; index++)
            {
                Assert.Equal(cached[index], await repository.FindGenerationAsync(id, index, CancellationToken.None));
            }
        }
    }

    private static bool[][] Blinker() =>
    [
        [false, false, false],
        [true, true, true],
        [false, false, false],
    ];
}
