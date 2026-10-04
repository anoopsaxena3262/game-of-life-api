namespace GameOfLife.Infrastructure.Persistence;

/// <summary>Stored seed. The board row itself is not rewritten after upload.</summary>
public sealed class BoardRecord
{
    public Guid Id { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public string Topology { get; set; } = "Bounded";

    public byte[] SeedCells { get; set; } = [];

    public long SeedHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public BoardOutcomeRecord? Outcome { get; set; }

    public ICollection<GenerationSnapshotRecord> Snapshots { get; set; } = [];
}

public sealed class GenerationSnapshotRecord
{
    public Guid BoardId { get; set; }

    public int GenerationIndex { get; set; }

    public byte[] Cells { get; set; } = [];

    public long StateHash { get; set; }

    public DateTimeOffset ComputedAt { get; set; }

    public BoardRecord Board { get; set; } = null!;
}

/// <summary>
/// Definitive outcome for a board, or progress toward one.
/// Outcome columns stay empty until the search proves a fixed point or a period,
/// so <see cref="SearchedThroughGeneration"/> can be saved before that.
/// </summary>
public sealed class BoardOutcomeRecord
{
    public Guid BoardId { get; set; }

    public string? OutcomeType { get; set; }

    public int? FixedPointAtGeneration { get; set; }

    public int? PeriodStart { get; set; }

    public int? Period { get; set; }

    public int SearchedThroughGeneration { get; set; }

    public DateTimeOffset? ComputedAt { get; set; }

    public BoardRecord Board { get; set; } = null!;
}
