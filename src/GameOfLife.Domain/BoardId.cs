namespace GameOfLife.Domain;

/// <summary>Time-ordered identifier for an uploaded board.</summary>
public readonly record struct BoardId(Guid Value)
{
    public static BoardId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}
