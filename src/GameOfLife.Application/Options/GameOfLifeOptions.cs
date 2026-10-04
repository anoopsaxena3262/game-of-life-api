namespace GameOfLife.Application;

/// <summary>Limits and switches bound from configuration. Values here are the defaults.</summary>
public sealed class GameOfLifeOptions
{
    public const string SectionName = "GameOfLife";

    public int MaxBoardCells { get; set; } = 1_000_000;

    public int MaxGenerationsPerRequest { get; set; } = 100_000;

    public int MaxSyncGenerationLimit { get; set; } = 50_000;

    public int DefaultGenerationLimit { get; set; } = 1_000;

    public int SnapshotInterval { get; set; } = 100;

    public bool TreatPeriodicAsTerminal { get; set; }
}
