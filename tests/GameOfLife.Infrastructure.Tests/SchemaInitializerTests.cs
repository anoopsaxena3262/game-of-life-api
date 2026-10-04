using GameOfLife.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameOfLife.Infrastructure.Tests;

/// <summary>Pins the stored schema: exactly the board and generation tables, as designed.</summary>
public sealed class SchemaInitializerTests : IDisposable
{
    private readonly TempDatabase _database = new(Path.Combine("nested", "dir", "boards.db"));

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task Creates_the_missing_directory_and_exactly_the_two_tables()
    {
        await InitializeAsync();

        Assert.True(File.Exists(_database.FilePath));
        var tables = await QueryAsync(
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name",
            reader => reader.GetString(0));
        Assert.Equal(["board", "generation"], tables);
    }

    [Fact]
    public async Task Board_table_has_the_expected_columns()
    {
        await InitializeAsync();

        var columns = await TableInfoAsync("board");

        Assert.Equal(
            [
                ("id", "TEXT", false, 1),
                ("width", "INTEGER", true, 0),
                ("height", "INTEGER", true, 0),
                ("initial_state", "TEXT", true, 0),
                ("created_at", "TEXT", true, 0),
                ("max_generations", "INTEGER", false, 0),
            ],
            columns);
    }

    [Fact]
    public async Task Generation_table_has_a_composite_key_and_a_foreign_key_to_board()
    {
        await InitializeAsync();

        var columns = await TableInfoAsync("generation");
        Assert.Equal(
            [
                ("board_id", "TEXT", true, 1),
                ("idx", "INTEGER", true, 2),
                ("state", "TEXT", true, 0),
            ],
            columns);

        var foreignKeys = await QueryAsync(
            "SELECT \"table\", \"from\", \"to\" FROM pragma_foreign_key_list('generation')",
            reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2)));
        Assert.Equal([("board", "board_id", "id")], foreignKeys);
    }

    [Fact]
    public async Task Uses_wal_and_turns_foreign_keys_on_for_each_connection()
    {
        await InitializeAsync();

        await using var connection = await _database.Connections().OpenAsync(CancellationToken.None);
        Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode"));
        Assert.Equal(1L, await ScalarAsync(connection, "PRAGMA foreign_keys"));
    }

    [Fact]
    public async Task Running_twice_keeps_existing_rows()
    {
        var repository = await _database.InitializedRepositoryAsync();
        var board = new GameOfLife.Domain.Board(Guid.NewGuid(), 1, 1, "1", DateTimeOffset.UtcNow, null);
        await repository.SaveAsync(board, CancellationToken.None);

        await InitializeAsync();

        Assert.NotNull(await repository.FindByIdAsync(board.Id, CancellationToken.None));
    }

    private Task InitializeAsync() =>
        new SchemaInitializer(_database.Connections(), NullLogger<SchemaInitializer>.Instance)
            .InitializeAsync(CancellationToken.None);

    private Task<List<(string Name, string Type, bool NotNull, long PrimaryKeyPosition)>> TableInfoAsync(string table) =>
        QueryAsync(
            $"SELECT name, type, \"notnull\", pk FROM pragma_table_info('{table}') ORDER BY cid",
            reader => (reader.GetString(0), reader.GetString(1), reader.GetInt64(2) == 1, reader.GetInt64(3)));

    private async Task<List<T>> QueryAsync<T>(string sql, Func<SqliteDataReader, T> map)
    {
        await using var connection = new SqliteConnection(_database.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<T>();
        while (await reader.ReadAsync())
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }
}
