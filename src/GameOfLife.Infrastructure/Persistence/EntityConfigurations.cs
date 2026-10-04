using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GameOfLife.Infrastructure.Persistence;

public sealed class BoardRecordConfiguration : IEntityTypeConfiguration<BoardRecord>
{
    public void Configure(EntityTypeBuilder<BoardRecord> builder)
    {
        builder.ToTable("Boards");
        builder.HasKey(board => board.Id);
        builder.Property(board => board.Topology).HasMaxLength(16).IsRequired();
        builder.Property(board => board.SeedCells).IsRequired();
        builder.Property(board => board.RowVersion).IsRowVersion();
        builder.HasOne(board => board.Outcome)
            .WithOne(outcome => outcome.Board)
            .HasForeignKey<BoardOutcomeRecord>(outcome => outcome.BoardId);
    }
}

public sealed class GenerationSnapshotRecordConfiguration : IEntityTypeConfiguration<GenerationSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<GenerationSnapshotRecord> builder)
    {
        builder.ToTable("GenerationSnapshots");
        builder.HasKey(snapshot => new { snapshot.BoardId, snapshot.GenerationIndex });
        builder.Property(snapshot => snapshot.Cells).IsRequired();
        builder.HasOne(snapshot => snapshot.Board)
            .WithMany(board => board.Snapshots)
            .HasForeignKey(snapshot => snapshot.BoardId);
    }
}

public sealed class BoardOutcomeRecordConfiguration : IEntityTypeConfiguration<BoardOutcomeRecord>
{
    public void Configure(EntityTypeBuilder<BoardOutcomeRecord> builder)
    {
        builder.ToTable("BoardOutcomes");
        builder.HasKey(outcome => outcome.BoardId);
        builder.Property(outcome => outcome.OutcomeType).HasMaxLength(32);
    }
}
