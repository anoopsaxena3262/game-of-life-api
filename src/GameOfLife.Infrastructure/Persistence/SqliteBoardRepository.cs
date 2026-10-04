using System.Globalization;
using GameOfLife.Application;
using GameOfLife.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>
/// SQLite-backed implementation. Plain ADO.NET rather than an ORM: two tables do not
/// justify one, and INSERT OR IGNORE is the write the cache needs.
/// </summary>
public sealed class SqliteBoardRepository(SqliteConnectionFactory connections, ILogger<SqliteBoardRepository> logger)
    : IBoardRepository
{
    // ISO-8601 UTC with a trailing Z, trailing fractional zeros dropped: 2026-01-02T03:04:05Z.
    private const string CreatedAtFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'";

    /// <summary>
    /// Writes the board and generation 0 in one transaction. A crash between the two
    /// inserts must not leave a board whose generation 0 row is missing.
    /// </summary>
    public async Task SaveAsync(Board board, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "save board id={Id} width={Width} height={Height} maxGenerations={MaxGenerations}",
            board.Id, board.Width, board.Height, board.MaxGenerations);

        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var insertBoard = connection.CreateCommand())
        {
            insertBoard.Transaction = transaction;
            insertBoard.CommandText = """
                INSERT INTO board (id, width, height, initial_state, created_at, max_generations)
                VALUES ($id, $width, $height, $initialState, $createdAt, $maxGenerations)
                """;
            insertBoard.Parameters.AddWithValue("$id", FormatId(board.Id));
            insertBoard.Parameters.AddWithValue("$width", board.Width);
            insertBoard.Parameters.AddWithValue("$height", board.Height);
            insertBoard.Parameters.AddWithValue("$initialState", board.InitialState);
            insertBoard.Parameters.AddWithValue("$createdAt", FormatCreatedAt(board.CreatedAt));
            insertBoard.Parameters.AddWithValue("$maxGenerations", (object?)board.MaxGenerations ?? DBNull.Value);
            await insertBoard.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertGeneration = connection.CreateCommand())
        {
            insertGeneration.Transaction = transaction;
            insertGeneration.CommandText = """
                INSERT INTO generation (board_id, idx, state)
                VALUES ($boardId, 0, $state)
                """;
            insertGeneration.Parameters.AddWithValue("$boardId", FormatId(board.Id));
            insertGeneration.Parameters.AddWithValue("$state", board.InitialState);
            await insertGeneration.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Board?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        logger.LogDebug("findById id={Id}", id);

        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, width, height, initial_state, created_at, max_generations
            FROM board
            WHERE id = $id
            """;
        command.Parameters.AddWithValue("$id", FormatId(id));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new Board(
            Guid.Parse(reader.GetString(0)),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetString(3),
            ParseCreatedAt(reader.GetString(4)),
            // The column is optional. Keep NULL as null rather than 0.
            reader.IsDBNull(5) ? null : reader.GetInt32(5));
    }

    public async Task<string?> FindGenerationAsync(Guid boardId, int index, CancellationToken cancellationToken)
    {
        logger.LogDebug("findGeneration boardId={BoardId} index={Index}", boardId, index);

        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT state
            FROM generation
            WHERE board_id = $boardId AND idx = $idx
            """;
        command.Parameters.AddWithValue("$boardId", FormatId(boardId));
        command.Parameters.AddWithValue("$idx", index);

        return await command.ExecuteScalarAsync(cancellationToken) as string;
    }

    public async Task<int?> FindHighestCachedIndexAsync(Guid boardId, CancellationToken cancellationToken)
    {
        logger.LogDebug("findHighestCachedIndex boardId={BoardId}", boardId);

        // Generation 0 is written with the board, so it is not a computed generation.
        // MAX over no computed rows is NULL, which means the service starts again from 0.
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT MAX(idx)
            FROM generation
            WHERE board_id = $boardId AND idx > 0
            """;
        command.Parameters.AddWithValue("$boardId", FormatId(boardId));

        var highest = await command.ExecuteScalarAsync(cancellationToken);
        return highest is null or DBNull ? null : Convert.ToInt32(highest, CultureInfo.InvariantCulture);
    }

    public async Task SaveGenerationAsync(Guid boardId, int index, string state, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "saveGeneration boardId={BoardId} index={Index} stateLength={StateLength}",
            boardId, index, state.Length);

        // IGNORE, not REPLACE. Two requests can compute the same generation at once.
        // The row for a given (board_id, idx) never changes, so the second insert is a no-op.
        await using var connection = await connections.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT OR IGNORE INTO generation (board_id, idx, state)
            VALUES ($boardId, $idx, $state)
            """;
        command.Parameters.AddWithValue("$boardId", FormatId(boardId));
        command.Parameters.AddWithValue("$idx", index);
        command.Parameters.AddWithValue("$state", state);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Lowercase, hyphenated. Bound as text so the driver never stores a Guid as a BLOB.
    private static string FormatId(Guid id) => id.ToString("D");

    private static string FormatCreatedAt(DateTimeOffset createdAt) =>
        createdAt.UtcDateTime.ToString(CreatedAtFormat, CultureInfo.InvariantCulture);

    private static DateTimeOffset ParseCreatedAt(string value) =>
        DateTimeOffset.Parse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
