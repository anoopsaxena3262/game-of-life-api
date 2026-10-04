using GameOfLife.Application;
using GameOfLife.Domain;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class BoardRepository : IBoardRepository
{
    public Task<Board?> FindAsync(BoardId id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task AddAsync(Board board, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public sealed class GenerationCache : IGenerationCache
{
    public Task<Generation?> FindAsync(BoardId boardId, int generation, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task SaveAsync(BoardId boardId, Generation generation, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
