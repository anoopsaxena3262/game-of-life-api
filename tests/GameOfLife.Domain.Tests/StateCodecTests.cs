namespace GameOfLife.Domain.Tests;

public sealed class StateCodecTests
{
    [Fact]
    public void Round_trip_preserves_an_arbitrary_board()
    {
        var random = new Random(42);
        int[][] sizes = [[1, 1], [1, 8], [8, 1], [3, 5], [17, 4], [10, 10]];
        foreach (var size in sizes)
        {
            var rows = size[0];
            var cols = size[1];
            var grid = RandomGrid(random, rows, cols);

            var encoded = StateCodec.Serialize(grid);

            Assert.Equal(rows * cols, encoded.Length);
            Assert.Matches("^[01]+$", encoded);
            var restored = StateCodec.Deserialize(encoded, cols, rows);
            Assert.Equal(grid, restored);
            Assert.Equal(encoded, StateCodec.Serialize(restored));
        }
    }

    [Fact]
    public void Serialises_a_horizontal_blinker_to_000111000()
    {
        bool[][] horizontalBlinker =
        [
            [false, false, false],
            [true, true, true],
            [false, false, false],
        ];

        Assert.Equal("000111000", StateCodec.Serialize(horizontalBlinker));
        Assert.Equal(horizontalBlinker, StateCodec.Deserialize("000111000", 3, 3));
    }

    [Fact]
    public void Encodes_cells_row_major_left_to_right_then_top_to_bottom()
    {
        bool[][] grid =
        [
            [true, false],
            [false, true],
        ];

        Assert.Equal("1001", StateCodec.Serialize(grid));
        Assert.Equal(grid, StateCodec.Deserialize("1001", 2, 2));
    }

    [Fact]
    public void Rejects_a_state_whose_length_does_not_match_width_times_height()
    {
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("01", 3, 3));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("0001110000", 3, 3));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("", 1, 1));
    }

    [Fact]
    public void Rejects_a_state_containing_a_character_other_than_0_or_1()
    {
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("2", 1, 1));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("000111002", 3, 3));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize(" ", 1, 1));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("00011100a", 3, 3));
    }

    [Fact]
    public void Rejects_a_null_state_and_non_positive_dimensions()
    {
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize(null, 1, 1));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("0", 0, 1));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("0", -1, 1));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("0", 1, 0));
        Assert.Throws<ArgumentException>(() => StateCodec.Deserialize("0", 1, -2));
    }

    [Fact]
    public void Rejects_a_null_empty_jagged_or_zero_width_grid()
    {
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize(null));
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize([]));
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize([[]]));
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize([null]));
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize([[true, false], null]));
        Assert.Throws<ArgumentException>(() => StateCodec.Serialize([[true, false], [true]]));
    }

    [Fact]
    public void Is_extinct_only_when_the_state_contains_no_live_cell()
    {
        Assert.True(StateCodec.IsExtinct("000000000"));
        Assert.True(StateCodec.IsExtinct(""));
        Assert.False(StateCodec.IsExtinct("000111000"));
        Assert.False(StateCodec.IsExtinct("1"));
        Assert.True(StateCodec.IsExtinct(StateCodec.Serialize(
        [
            [false, false],
            [false, false],
        ])));
        Assert.Throws<ArgumentException>(() => StateCodec.IsExtinct(null));
    }

    private static bool[][] RandomGrid(Random random, int rows, int cols)
    {
        var grid = new bool[rows][];
        for (var row = 0; row < rows; row++)
        {
            grid[row] = new bool[cols];
            for (var col = 0; col < cols; col++)
            {
                grid[row][col] = random.Next(2) == 1;
            }
        }

        return grid;
    }
}
