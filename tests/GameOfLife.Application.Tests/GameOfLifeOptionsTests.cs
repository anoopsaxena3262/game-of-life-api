namespace GameOfLife.Application.Tests;

public sealed class GameOfLifeOptionsTests
{
    private readonly GameOfLifeOptionsValidator _validator = new();

    [Fact]
    public void Defaults_match_the_configured_caps()
    {
        var options = new GameOfLifeOptions();

        Assert.Equal(1_000, options.MaxGenerations);
        Assert.Equal(10_000, options.MaxGenerationsCeiling);
        Assert.Equal(90_000, options.MaxCells);
        Assert.Equal(5_000_000, options.MaxCellGenerations);
        Assert.Equal(2_000_000, options.MaxRequestBytes);
        Assert.True(_validator.Validate(null, options).Succeeded);
    }

    [Theory]
    [InlineData(nameof(GameOfLifeOptions.MaxGenerations), "GameOfLife:MaxGenerations must be at least 1")]
    [InlineData(nameof(GameOfLifeOptions.MaxCells), "GameOfLife:MaxCells must be at least 1")]
    [InlineData(nameof(GameOfLifeOptions.MaxCellGenerations), "GameOfLife:MaxCellGenerations must be at least 1")]
    [InlineData(nameof(GameOfLifeOptions.MaxRequestBytes), "GameOfLife:MaxRequestBytes must be at least 1")]
    public void Rejects_a_limit_below_1(string property, string message)
    {
        var options = property switch
        {
            nameof(GameOfLifeOptions.MaxGenerations) => new GameOfLifeOptions { MaxGenerations = 0 },
            nameof(GameOfLifeOptions.MaxCells) => new GameOfLifeOptions { MaxCells = 0 },
            nameof(GameOfLifeOptions.MaxCellGenerations) => new GameOfLifeOptions { MaxCellGenerations = 0 },
            nameof(GameOfLifeOptions.MaxRequestBytes) => new GameOfLifeOptions { MaxRequestBytes = 0 },
            _ => throw new ArgumentOutOfRangeException(nameof(property)),
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(message, result.Failures!);
    }

    [Fact]
    public void Rejects_a_ceiling_below_the_default()
    {
        var options = new GameOfLifeOptions { MaxGenerations = 500, MaxGenerationsCeiling = 499 };

        var result = _validator.Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("GameOfLife:MaxGenerationsCeiling must be at least MaxGenerations", result.Failures!);
    }

    [Fact]
    public void Accepts_a_ceiling_equal_to_the_default()
    {
        var options = new GameOfLifeOptions { MaxGenerations = 500, MaxGenerationsCeiling = 500 };

        Assert.True(_validator.Validate(null, options).Succeeded);
    }
}
