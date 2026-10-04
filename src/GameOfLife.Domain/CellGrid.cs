namespace GameOfLife.Domain;

/// <summary>
/// A rectangular grid of cells. Storage and neighbour arithmetic are not implemented yet.
/// </summary>
public sealed class CellGrid
{
    public CellGrid(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfNegative(height);
        Width = width;
        Height = height;
    }

    public int Width { get; }

    public int Height { get; }

    public bool this[int x, int y]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public int NeighborCount(int x, int y) => throw new NotImplementedException();
}
