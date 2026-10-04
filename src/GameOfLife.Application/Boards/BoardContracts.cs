namespace GameOfLife.Application;

public sealed record CreateBoardCommand(
    int Width,
    int Height,
    string? Topology = null,
    IReadOnlyList<string>? Cells = null,
    IReadOnlyList<IReadOnlyList<int>>? LiveCells = null,
    string? IdempotencyKey = null);

public sealed record BoardCreated(Guid Id, int Width, int Height, int Generation);

public sealed record BoardView(
    Guid Id,
    int Width,
    int Height,
    string Topology,
    IReadOnlyList<string> Cells,
    int Generation,
    string StateHash);

public sealed record FinalStateView(
    BoardView Board,
    string Outcome,
    int? FixedPointAtGeneration,
    int? PeriodStart,
    int? Period,
    int? GenerationsComputed);
