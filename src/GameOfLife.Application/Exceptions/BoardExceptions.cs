namespace GameOfLife.Application;

/// <summary>No board exists with the requested id. Surfaces as 404.</summary>
public sealed class BoardNotFoundException(Guid id) : Exception($"No board with id {id}")
{
    public Guid BoardId { get; } = id;
}

/// <summary>The uploaded board, generation index, or maxGenerations value is not acceptable. Surfaces as 400.</summary>
public sealed class InvalidBoardException(string message) : Exception(message);

/// <summary>
/// The board reached neither a fixed point nor a cycle within the generation limit.
/// Surfaces as 422, not 500: hitting the limit is a documented outcome of the request,
/// not a server fault.
/// </summary>
public sealed class NoConclusionException(int generationsAttempted)
    : Exception($"No conclusion reached within {generationsAttempted} generations")
{
    public int GenerationsAttempted { get; } = generationsAttempted;
}
