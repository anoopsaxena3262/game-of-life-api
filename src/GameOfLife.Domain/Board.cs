namespace GameOfLife.Domain;

/// <summary>
/// An uploaded board. Immutable once created: generation 0 is never overwritten,
/// and advancing the simulation produces new states rather than changing this record.
/// </summary>
/// <param name="InitialState">Generation 0 as a flat row-major '0'/'1' string of length width * height.</param>
/// <param name="MaxGenerations">Optional stored cap for /final. Null means the configured default.</param>
public sealed record Board(
    Guid Id,
    int Width,
    int Height,
    string InitialState,
    DateTimeOffset CreatedAt,
    int? MaxGenerations);
