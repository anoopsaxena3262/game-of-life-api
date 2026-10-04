namespace GameOfLife.Domain;

/// <summary>How a board concluded.</summary>
public enum TerminationKind
{
    /// <summary>Every cell is dead. Reported separately from <see cref="FixedPoint"/> for clearer semantics.</summary>
    Extinct,

    /// <summary>The next generation is identical to the current one (period 1).</summary>
    FixedPoint,

    /// <summary>A previously seen generation recurred, with period greater than 1.</summary>
    Cycle,
}
