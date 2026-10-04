using Microsoft.EntityFrameworkCore;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class GameOfLifeDbContext(DbContextOptions<GameOfLifeDbContext> options) : DbContext(options)
{
    public DbSet<BoardRecord> Boards => Set<BoardRecord>();

    public DbSet<GenerationSnapshotRecord> GenerationSnapshots => Set<GenerationSnapshotRecord>();

    public DbSet<BoardOutcomeRecord> BoardOutcomes => Set<BoardOutcomeRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GameOfLifeDbContext).Assembly);
    }
}
