using GameOfLife.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameOfLife.Application;

/// <summary>A concluded board and the generation cap the walk actually used.</summary>
/// <param name="GenerationsLimit">After the ceiling clamp.</param>
public sealed record FinalStateOutcome(TerminationResult Result, int GenerationsLimit);

/// <summary>
/// Orchestration, memoisation and limit policy.
/// </summary>
/// <remarks>
/// Reads are pure. Fetching generation N never advances the stored board: boards are
/// immutable after upload, and the only writes a GET performs go to the generation cache,
/// which is invisible through the API. This keeps the endpoints idempotent: calling
/// /next twice returns generation 1 both times.
/// </remarks>
public sealed class BoardService(
    IBoardRepository repository,
    IOptions<GameOfLifeOptions> options,
    TimeProvider timeProvider,
    ILogger<BoardService> logger)
{
    private readonly GameOfLifeOptions _limits = options.Value;

    /// <summary>Validates and stores a new board.</summary>
    /// <returns>The generated board id.</returns>
    /// <exception cref="InvalidBoardException">The grid is malformed or over the cell cap.</exception>
    public async Task<Guid> CreateAsync(int width, int height, bool[]?[]? grid, CancellationToken cancellationToken)
    {
        if (width <= 0 || height <= 0)
        {
            throw new InvalidBoardException("Width and height must be positive");
        }

        if (grid is null || grid.Length != height)
        {
            throw new InvalidBoardException("Grid height does not match declared height");
        }

        foreach (var row in grid)
        {
            if (row is null || row.Length != width)
            {
                throw new InvalidBoardException("Grid width does not match declared width");
            }
        }

        if ((long)width * height > _limits.MaxCells)
        {
            throw new InvalidBoardException($"Board exceeds maximum cell count of {_limits.MaxCells}");
        }

        var initialState = StateCodec.Serialize(grid);
        var id = Guid.NewGuid();
        // Upload does not accept a per-board generation cap, so it stays null and /final
        // uses the query parameter or the configured default.
        var board = new Board(id, width, height, initialState, timeProvider.GetUtcNow(), null);

        await repository.SaveAsync(board, cancellationToken);
        logger.LogInformation("created board id={Id} width={Width} height={Height}", id, width, height);
        return id;
    }

    /// <exception cref="BoardNotFoundException">No board has this id.</exception>
    public async Task<Board> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw new BoardNotFoundException(id);

    /// <summary>Returns the state <paramref name="index"/> generations after upload. Pure read.</summary>
    /// <param name="index">0 returns the uploaded board unchanged.</param>
    /// <exception cref="InvalidBoardException">The index is negative, past the ceiling, or over the cell-generation budget.</exception>
    /// <exception cref="BoardNotFoundException">No board has this id.</exception>
    public async Task<string> GenerationAtAsync(Guid id, int index, CancellationToken cancellationToken)
    {
        if (index < 0)
        {
            throw new InvalidBoardException("Generation index cannot be negative");
        }

        if (index > _limits.MaxGenerationsCeiling)
        {
            throw new InvalidBoardException($"Generation index exceeds ceiling of {_limits.MaxGenerationsCeiling}");
        }

        var cached = await repository.FindGenerationAsync(id, index, cancellationToken);
        if (cached is not null)
        {
            logger.LogDebug("generationAt id={Id} index={Index} cache hit", id, index);
            return cached;
        }

        // Cache miss. Resume from the highest cached index that is still at or before
        // the one we were asked for. A later cached row is a different generation.
        var board = await GetAsync(id, cancellationToken);
        var cells = (long)board.Width * board.Height;
        if (index * cells > _limits.MaxCellGenerations)
        {
            throw new InvalidBoardException(
                $"Generation {index} on a board of {cells} cells exceeds the cell-generation budget of {_limits.MaxCellGenerations}");
        }

        var highest = await repository.FindHighestCachedIndexAsync(id, cancellationToken) ?? 0;
        var startIndex = highest;
        if (highest > index)
        {
            logger.LogWarning(
                "generationAt id={Id} cached index {Highest} is past requested {Index}; resuming from 0",
                id, highest, index);
            startIndex = 0;
        }

        logger.LogDebug("generationAt id={Id} index={Index} resuming from {StartIndex}", id, index, startIndex);
        var current = await repository.FindGenerationAsync(id, startIndex, cancellationToken)
            ?? (startIndex == 0
                ? board.InitialState
                : throw new InvalidOperationException(
                    $"Generation cache for board {id} is missing index {startIndex}"));

        for (var step = startIndex; step < index; step++)
        {
            current = LifeEngine.Step(current, board.Width, board.Height);
            await repository.SaveGenerationAsync(id, step + 1, current, cancellationToken);
        }

        return current;
    }

    /// <summary>Walks the board to extinction, a fixed point or a cycle.</summary>
    /// <param name="requestedMax">
    /// Caller override. Below 1 is rejected. Above the ceiling is clamped.
    /// Null uses the board's stored cap, or the configured default when that is null.
    /// </param>
    /// <exception cref="InvalidBoardException"><paramref name="requestedMax"/> is below 1.</exception>
    /// <exception cref="NoConclusionException">The limit was reached.</exception>
    /// <exception cref="BoardNotFoundException">No board has this id.</exception>
    public async Task<FinalStateOutcome> FinalStateAsync(Guid id, int? requestedMax, CancellationToken cancellationToken)
    {
        var board = await GetAsync(id, cancellationToken);
        var limit = ResolveLimit(board, requestedMax);

        var result = TerminationDetector.Detect(board.InitialState, board.Width, board.Height, limit)
            ?? throw new NoConclusionException(limit);
        logger.LogInformation(
            "finalState id={Id} kind={Kind} period={Period} firstOccurrence={FirstOccurrence} generations={Generations} limit={Limit}",
            id, result.Kind, result.Period, result.FirstOccurrence, result.GenerationsComputed, limit);
        return new FinalStateOutcome(result, limit);
    }

    /// <summary>
    /// Caller override, then a stored per-board cap, then the configured default. Upload
    /// never sets the stored cap, so the middle branch is for a row written outside this API.
    /// The ceiling then clamps that number; it is visible as generationsLimit. The
    /// cell-generation budget does not apply here: /final writes no rows, it only keeps a
    /// hash per step. The budget is for /generations, which stores every state.
    /// </summary>
    private int ResolveLimit(Board board, int? requestedMax)
    {
        if (requestedMax < 1)
        {
            throw new InvalidBoardException("maxGenerations must be at least 1");
        }

        var limit = requestedMax ?? board.MaxGenerations ?? _limits.MaxGenerations;
        if (requestedMax > _limits.MaxGenerationsCeiling)
        {
            logger.LogWarning(
                "finalState id={Id} maxGenerations {Requested} clamped to ceiling {Ceiling}",
                board.Id, requestedMax, _limits.MaxGenerationsCeiling);
        }

        return Math.Min(limit, _limits.MaxGenerationsCeiling);
    }
}
