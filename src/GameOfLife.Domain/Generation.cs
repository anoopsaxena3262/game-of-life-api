namespace GameOfLife.Domain;

/// <summary>One immutable generation of a board, derived from its seed.</summary>
public sealed record Generation(int Index, CellGrid Cells, ulong StateHash);
