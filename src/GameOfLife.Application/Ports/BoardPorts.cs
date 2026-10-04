using GameOfLife.Domain;

namespace GameOfLife.Application;

/// <summary>
/// Storage contract for boards and their memoised generations. It is the extension point
/// for moving off SQLite to a server-backed store.
/// </summary>
public interface IBoardRepository
{
    /// <summary>Writes the board and its generation 0 together.</summary>
    Task SaveAsync(Board board, CancellationToken cancellationToken);

    Task<Board?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <returns>The memoised state at <paramref name="index"/>, or null if it has not been computed yet.</returns>
    Task<string?> FindGenerationAsync(Guid boardId, int index, CancellationToken cancellationToken);

    /// <returns>
    /// The highest generation index already cached for this board, or null when only generation 0
    /// exists. Lets the service resume rather than restart.
    /// </returns>
    Task<int?> FindHighestCachedIndexAsync(Guid boardId, CancellationToken cancellationToken);

    /// <summary>
    /// Idempotent. Two requests racing to compute the same generation produce the same row,
    /// so the write is allowed to collide harmlessly.
    /// </summary>
    Task SaveGenerationAsync(Guid boardId, int index, string state, CancellationToken cancellationToken);
}
