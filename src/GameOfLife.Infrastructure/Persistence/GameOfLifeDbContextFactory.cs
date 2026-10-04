using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GameOfLife.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c> so migrations do not need the web host.</summary>
public sealed class GameOfLifeDbContextFactory : IDesignTimeDbContextFactory<GameOfLifeDbContext>
{
    public GameOfLifeDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GameOfLifeDbContext>()
            .UseSqlite("Data Source=game-of-life.design.db")
            .Options;
        return new GameOfLifeDbContext(options);
    }
}
