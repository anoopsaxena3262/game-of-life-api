using GameOfLife.Application;

namespace GameOfLife.Application.Tests;

public sealed class GameOfLifeOptionsTests
{
    [Fact]
    public void Defaults_match_the_configured_caps()
    {
        var options = new GameOfLifeOptions();

        Assert.Equal(1_000_000, options.MaxBoardCells);
        Assert.Equal(100_000, options.MaxGenerationsPerRequest);
        Assert.Equal(50_000, options.MaxSyncGenerationLimit);
        Assert.Equal(1_000, options.DefaultGenerationLimit);
        Assert.Equal(100, options.SnapshotInterval);
        Assert.False(options.TreatPeriodicAsTerminal);
    }
}
