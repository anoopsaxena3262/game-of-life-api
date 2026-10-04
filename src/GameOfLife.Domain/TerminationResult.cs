namespace GameOfLife.Domain;

/// <summary>Outcome of walking a board forward until it concludes.</summary>
/// <param name="Kind">How the board concluded.</param>
/// <param name="State">The state at the point of detection.</param>
/// <param name="FirstOccurrence">Generation index where this state was first seen.</param>
/// <param name="Period">1 for a fixed point or extinction, greater than 1 for a cycle.</param>
/// <param name="GenerationsComputed">How many generations were walked to get here.</param>
public sealed record TerminationResult(
    TerminationKind Kind,
    string State,
    int FirstOccurrence,
    int Period,
    int GenerationsComputed);
