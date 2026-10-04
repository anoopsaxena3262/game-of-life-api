using GameOfLife.Domain;
using Microsoft.Data.Sqlite;

namespace GameOfLife.Infrastructure.Tests;

public sealed class SqliteBoardRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset CreatedAt = DateTimeOffset.Parse("2026-01-02T03:04:05Z");

    private readonly TempDatabase _database = new();

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Saves_and_reads_back_a_board()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var withLimit = NewBoard(40);
        var withoutLimit = NewBoard(null);

        await repository.SaveAsync(withLimit, CancellationToken.None);
        await repository.SaveAsync(withoutLimit, CancellationToken.None);

        Assert.Equal(withLimit, await repository.FindByIdAsync(withLimit.Id, CancellationToken.None));
        Assert.Equal(withoutLimit, await repository.FindByIdAsync(withoutLimit.Id, CancellationToken.None));
        Assert.Equal(withLimit.InitialState, await repository.FindGenerationAsync(withLimit.Id, 0, CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_board_id()
    {
        var repository = await _database.InitializedRepositoryAsync();

        Assert.Null(await repository.FindByIdAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Caches_and_retrieves_a_generation_by_index()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var board = NewBoard(null);
        await repository.SaveAsync(board, CancellationToken.None);

        await repository.SaveGenerationAsync(board.Id, 2, "111000111", CancellationToken.None);

        Assert.Equal("111000111", await repository.FindGenerationAsync(board.Id, 2, CancellationToken.None));
        Assert.Null(await repository.FindGenerationAsync(board.Id, 9, CancellationToken.None));
        Assert.Equal(2, await repository.FindHighestCachedIndexAsync(board.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Saving_the_same_generation_twice_is_idempotent()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var board = NewBoard(null);
        await repository.SaveAsync(board, CancellationToken.None);

        await repository.SaveGenerationAsync(board.Id, 1, "111000111", CancellationToken.None);
        await repository.SaveGenerationAsync(board.Id, 1, "000111000", CancellationToken.None);

        Assert.Equal("111000111", await repository.FindGenerationAsync(board.Id, 1, CancellationToken.None));
    }

    [Fact]
    public async Task Highest_cached_index_is_null_for_a_board_with_no_computed_generations()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var board = NewBoard(null);
        await repository.SaveAsync(board, CancellationToken.None);

        Assert.Null(await repository.FindHighestCachedIndexAsync(board.Id, CancellationToken.None));
        Assert.Null(await repository.FindHighestCachedIndexAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task Stores_the_id_as_lowercase_text_and_created_at_as_utc_iso_8601()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var board = NewBoard(null) with
        {
            CreatedAt = new DateTimeOffset(2026, 1, 2, 5, 4, 5, 120, TimeSpan.FromHours(2)),
        };
        await repository.SaveAsync(board, CancellationToken.None);

        await using var connection = new SqliteConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT typeof(id), id, created_at, typeof(max_generations) FROM board";
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        Assert.Equal("text", reader.GetString(0));
        Assert.Equal(board.Id.ToString("D"), reader.GetString(1));
        Assert.Equal("2026-01-02T03:04:05.12Z", reader.GetString(2));
        Assert.Equal("null", reader.GetString(3));
    }

    [Fact]
    public async Task A_generation_for_an_unknown_board_is_rejected_by_the_foreign_key()
    {
        var repository = await _database.InitializedRepositoryAsync();

        var error = await Assert.ThrowsAsync<SqliteException>(() =>
            repository.SaveGenerationAsync(Guid.NewGuid(), 1, "000", CancellationToken.None));

        Assert.Contains("FOREIGN KEY", error.Message);
    }

    private static Board NewBoard(int? maxGenerations) =>
        new(Guid.NewGuid(), 3, 3, "000111000", CreatedAt, maxGenerations);
}
