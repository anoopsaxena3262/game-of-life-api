using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>
/// Creates the schema at startup. No migration tool: two tables with no versioned
/// history do not justify one.
/// </summary>
public sealed class SchemaInitializer(SqliteConnectionFactory connections, ILogger<SchemaInitializer> logger)
{
    // board comes first. The generation table points at board.id, so the board table
    // has to exist already or the foreign key line fails.
    internal const string CreateBoardTable = """
        CREATE TABLE IF NOT EXISTS board (
                -- One row for each board the user uploads.
                -- When someone asks for the next generation we do not change this row.
                -- The new states go in the generation table.

                -- Id we make on the server, saved as text. This is the id the API gives back.
                -- The caller does not send an id.
                id TEXT PRIMARY KEY,
                -- How many columns and how many rows. This does not change after upload.
                -- initial_state must have width * height characters.
                width INTEGER NOT NULL,
                height INTEGER NOT NULL,
                -- The starting grid, generation 0. One string, row by row. 1 means alive, 0 means dead.
                -- We use this same string to notice when a state comes back again (a cycle).
                -- A 3x3 blinker when it is horizontal looks like 000111000.
                initial_state TEXT NOT NULL,
                -- Time of the upload, ISO-8601 UTC text.
                -- Helps when we open the db file and want to see which board came first.
                created_at TEXT NOT NULL,
                -- Optional stored cap for /final, used only when the request omits maxGenerations.
                -- The upload API does not set this. Rows created by POST /boards store NULL.
                -- NULL means use GameOfLife:MaxGenerations from appsettings.json at request time.
                -- Changing that default does change /final for boards that are already stored.
                max_generations INTEGER
            )
        """;

    internal const string CreateGenerationTable = """
        CREATE TABLE IF NOT EXISTS generation (
                -- States we already worked out, so the next read is just a lookup.
                -- After a restart the rows are still here and we do not compute them again.
                -- For the same board and the same index the state never changes.
                -- Save uses INSERT OR IGNORE, so two requests writing the same row is fine.
                -- /final does not read or write this table. It walks from board.initial_state in memory.

                -- Which board this state belongs to.
                -- If that id is not in the board table, the insert fails when foreign keys are on.
                board_id TEXT NOT NULL REFERENCES board(id),
                -- How many generations after the upload. 0 is the grid the user sent.
                -- We write 0 when we save the board, so reading generation 0 uses this table too.
                -- To continue a walk we take MAX(idx) for this board and go on from there.
                idx INTEGER NOT NULL,
                -- The grid as 0 and 1, same way as board.initial_state.
                state TEXT NOT NULL,
                -- Only one row for a board and a generation number. This is how we find a cached state.
                PRIMARY KEY (board_id, idx)
            )
        """;

    /// <summary>Creates the database directory and the two tables if they are not there yet.</summary>
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        CreateDatabaseDirectory(connections.DataSource);

        await using var connection = await connections.OpenAsync(cancellationToken);

        // WAL mode so if the process dies, the last commit is still on disk.
        // journal_mode is stored in the database file, so setting it once is enough.
        // It returns the new mode, so this is a query, not an update.
        await ExecuteScalarAsync(connection, "PRAGMA journal_mode=WAL", cancellationToken);
        await ExecuteAsync(connection, CreateBoardTable, cancellationToken);
        await ExecuteAsync(connection, CreateGenerationTable, cancellationToken);

        // IF NOT EXISTS means a second start does not fail. It also does not change a table
        // that is already there. To drop the old file first, run ./restart.sh and pick 2.
        logger.LogInformation("schema initialised successfully");
    }

    /// <summary>SQLite creates the database file, but not a missing parent directory.</summary>
    private static void CreateDatabaseDirectory(string dataSource)
    {
        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var directory = Path.GetDirectoryName(dataSource);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    private static async Task ExecuteAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ExecuteScalarAsync(
        SqliteConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
