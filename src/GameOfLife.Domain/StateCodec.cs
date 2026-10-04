using System.Text;

namespace GameOfLife.Domain;

/// <summary>
/// Converts between a boolean grid and the flat row-major '0'/'1' string used for
/// storage and as the cycle-detection key. A 3x3 blinker in its horizontal phase is
/// <c>"000111000"</c>.
/// </summary>
public static class StateCodec
{
    /// <summary>Flattens a grid to its string form, read row-major.</summary>
    /// <param name="grid">Indexed <c>[row][column]</c>.</param>
    /// <exception cref="ArgumentException">The grid is null, empty, jagged, or has a null row.</exception>
    public static string Serialize(bool[]?[]? grid)
    {
        if (grid is null)
        {
            throw new ArgumentException("Grid cannot be null");
        }

        var rows = grid.Length;
        if (rows == 0)
        {
            throw new ArgumentException("Grid cannot be empty");
        }

        var first = grid[0] ?? throw new ArgumentException("Row 0 is null");
        var cols = first.Length;
        if (cols == 0)
        {
            throw new ArgumentException("Grid cannot have zero columns");
        }

        // A jagged grid cannot be stored as one flat string of width * height.
        for (var i = 0; i < rows; i++)
        {
            var row = grid[i] ?? throw new ArgumentException($"Row {i} is null");
            if (row.Length != cols)
            {
                throw new ArgumentException($"Row {i} has length {row.Length} but expected {cols}");
            }
        }

        var sb = new StringBuilder(rows * cols);
        for (var i = 0; i < rows; i++)
        {
            foreach (var cell in grid[i]!)
            {
                sb.Append(cell ? '1' : '0');
            }
        }

        return sb.ToString();
    }

    /// <summary>Expands a flat state back into a grid indexed <c>[row][column]</c>.</summary>
    /// <exception cref="ArgumentException">
    /// The state is null, the dimensions are not positive, the length is not width * height,
    /// or it contains a character other than '0' or '1'.
    /// </exception>
    public static bool[][] Deserialize(string? state, int width, int height)
    {
        if (state is null)
        {
            throw new ArgumentException("State cannot be null");
        }

        if (width <= 0)
        {
            throw new ArgumentException($"Width must be positive, got: {width}");
        }

        if (height <= 0)
        {
            throw new ArgumentException($"Height must be positive, got: {height}");
        }

        // Check the length before indexing. A short string must be a 400 from the
        // caller, not an IndexOutOfRangeException that becomes a 500.
        var expectedLength = (long)width * height;
        if (state.Length != expectedLength)
        {
            throw new ArgumentException(
                $"State length {state.Length} does not match width * height ({expectedLength})");
        }

        for (var i = 0; i < state.Length; i++)
        {
            var c = state[i];
            if (c != '0' && c != '1')
            {
                throw new ArgumentException(
                    $"State contains invalid character '{c}' at position {i}, only '0' and '1' are allowed");
            }
        }

        var grid = new bool[height][];
        var index = 0;
        for (var row = 0; row < height; row++)
        {
            grid[row] = new bool[width];
            for (var col = 0; col < width; col++)
            {
                grid[row][col] = state[index++] == '1';
            }
        }

        return grid;
    }

    /// <summary>True when no cell in the state is alive.</summary>
    /// <exception cref="ArgumentException">The state is null.</exception>
    public static bool IsExtinct(string? state)
    {
        if (state is null)
        {
            throw new ArgumentException("State cannot be null");
        }

        // '1' is the only live marker. Absence of it means every cell is dead.
        return !state.Contains('1');
    }
}
