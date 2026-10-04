namespace GameOfLife.Domain;

/// <summary>An uploaded board. The seed does not change after creation.</summary>
public sealed record Board(
    BoardId Id,
    int Width,
    int Height,
    Topology Topology,
    Generation Seed,
    DateTimeOffset CreatedAt);
