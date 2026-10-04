using GameOfLife.Domain;

namespace GameOfLife.Application.Tests;

/// <summary>
/// In-memory repository that behaves like the SQLite one and records what the service asked for.
/// </summary>
internal sealed class FakeBoardRepository : IBoardRepository
{
    public Dictionary<Guid, Board> Boards { get; } = [];

    public Dictionary<(Guid BoardId, int Index), string> Generations { get; } = [];

    /// <summary>Forces the highest cached index, to model a cache that is missing a row.</summary>
    public int? HighestCachedIndexOverride { get; set; }

    public List<Board> Saved { get; } = [];

    public List<(Guid BoardId, int Index, string State)> SavedGenerations { get; } = [];

    public List<(Guid BoardId, int Index)> GenerationLookups { get; } = [];

    public int FindByIdCalls { get; private set; }

    public int FindHighestCachedIndexCalls { get; private set; }

    public Task SaveAsync(Board board, CancellationToken cancellationToken)
    {
        Saved.Add(board);
        Boards[board.Id] = board;
        Generations[(board.Id, 0)] = board.InitialState;
        return Task.CompletedTask;
    }

    public Task<Board?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        FindByIdCalls++;
        return Task.FromResult(Boards.GetValueOrDefault(id));
    }

    public Task<string?> FindGenerationAsync(Guid boardId, int index, CancellationToken cancellationToken)
    {
        GenerationLookups.Add((boardId, index));
        return Task.FromResult(Generations.GetValueOrDefault((boardId, index)));
    }

    public Task<int?> FindHighestCachedIndexAsync(Guid boardId, CancellationToken cancellationToken)
    {
        FindHighestCachedIndexCalls++;
        if (HighestCachedIndexOverride is { } forced)
        {
            return Task.FromResult<int?>(forced);
        }

        var computed = Generations.Keys
            .Where(key => key.BoardId == boardId && key.Index > 0)
            .Select(key => (int?)key.Index)
            .Max();
        return Task.FromResult(computed);
    }

    public Task SaveGenerationAsync(Guid boardId, int index, string state, CancellationToken cancellationToken)
    {
        SavedGenerations.Add((boardId, index, state));
        Generations.TryAdd((boardId, index), state);
        return Task.CompletedTask;
    }
}

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
