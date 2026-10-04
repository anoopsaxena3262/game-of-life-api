using GameOfLife.Domain;

namespace GameOfLife.Application;

/// <summary>Persistence for uploaded boards. The seed is the durable record.</summary>
public interface IBoardRepository
{
    Task<Board?> FindAsync(BoardId id, CancellationToken cancellationToken);

    Task AddAsync(Board board, CancellationToken cancellationToken);
}

/// <summary>Derived generation snapshots. Safe to drop; every generation can be recomputed.</summary>
public interface IGenerationCache
{
    Task<Generation?> FindAsync(BoardId boardId, int generation, CancellationToken cancellationToken);

    Task SaveAsync(BoardId boardId, Generation generation, CancellationToken cancellationToken);
}
