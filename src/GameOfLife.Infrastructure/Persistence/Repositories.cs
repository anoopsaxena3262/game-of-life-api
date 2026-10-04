using GameOfLife.Application;
using GameOfLife.Domain;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class BoardRepository : IBoardRepository
{
    public Task SaveAsync(Board board, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Board?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<string?> FindGenerationAsync(Guid boardId, int index, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<int?> FindHighestCachedIndexAsync(Guid boardId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task SaveGenerationAsync(Guid boardId, int index, string state, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
