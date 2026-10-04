namespace GameOfLife.Application;

public interface ICreateBoard
{
    Task<Result<BoardCreated>> HandleAsync(CreateBoardCommand command, CancellationToken cancellationToken);
}

public interface IGetBoard
{
    Task<Result<BoardView>> HandleAsync(Guid id, CancellationToken cancellationToken);
}

public interface IGetGeneration
{
    Task<Result<BoardView>> HandleAsync(Guid id, int generation, CancellationToken cancellationToken);
}

public interface IGetFinalState
{
    Task<Result<FinalStateView>> HandleAsync(Guid id, int? maxGenerations, CancellationToken cancellationToken);
}

public sealed class CreateBoardHandler : ICreateBoard
{
    public Task<Result<BoardCreated>> HandleAsync(CreateBoardCommand command, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public sealed class GetBoardHandler : IGetBoard
{
    public Task<Result<BoardView>> HandleAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public sealed class GetGenerationHandler : IGetGeneration
{
    public Task<Result<BoardView>> HandleAsync(Guid id, int generation, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

public sealed class GetFinalStateHandler : IGetFinalState
{
    public Task<Result<FinalStateView>> HandleAsync(Guid id, int? maxGenerations, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}
