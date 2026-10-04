namespace GameOfLife.Domain;

/// <summary>Conway's Game of Life rules, B3/S23. The step itself is not implemented yet.</summary>
public static class LifeEngine
{
    public static CellGrid Step(CellGrid grid, Topology topology) => throw new NotImplementedException();

    public static CellGrid Advance(CellGrid grid, Topology topology, int generations) =>
        throw new NotImplementedException();

    public static EvolutionOutcome RunToFixedPoint(
        CellGrid grid,
        Topology topology,
        int maxGenerations,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();
}
