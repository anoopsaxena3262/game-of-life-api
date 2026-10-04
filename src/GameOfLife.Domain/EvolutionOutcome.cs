namespace GameOfLife.Domain;

/// <summary>How a search for a fixed point ended.</summary>
public abstract record EvolutionOutcome
{
    private EvolutionOutcome()
    {
    }

    /// <summary>The next generation is identical to this one.</summary>
    public sealed record FixedPoint(int Generation, CellGrid Cells) : EvolutionOutcome;

    /// <summary>A state repeated with a period of 2 or more.</summary>
    public sealed record Periodic(int PeriodStart, int Period, CellGrid Cells) : EvolutionOutcome;

    /// <summary>The generation budget ended before either of the other outcomes.</summary>
    public sealed record Indeterminate(int GenerationsComputed, CellGrid Cells) : EvolutionOutcome;
}
