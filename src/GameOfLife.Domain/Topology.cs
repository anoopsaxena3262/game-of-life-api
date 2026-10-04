namespace GameOfLife.Domain;

/// <summary>How cells at the edge of a board find their neighbours.</summary>
public enum Topology
{
    Bounded,
    Toroidal
}
