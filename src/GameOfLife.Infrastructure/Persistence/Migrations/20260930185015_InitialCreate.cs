using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GameOfLife.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Boards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    Topology = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SeedCells = table.Column<byte[]>(type: "BLOB", nullable: false),
                    SeedHash = table.Column<long>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "BLOB", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Boards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BoardOutcomes",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    OutcomeType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    FixedPointAtGeneration = table.Column<int>(type: "INTEGER", nullable: true),
                    PeriodStart = table.Column<int>(type: "INTEGER", nullable: true),
                    Period = table.Column<int>(type: "INTEGER", nullable: true),
                    SearchedThroughGeneration = table.Column<int>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BoardOutcomes", x => x.BoardId);
                    table.ForeignKey(
                        name: "FK_BoardOutcomes_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GenerationSnapshots",
                columns: table => new
                {
                    BoardId = table.Column<Guid>(type: "TEXT", nullable: false),
                    GenerationIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Cells = table.Column<byte[]>(type: "BLOB", nullable: false),
                    StateHash = table.Column<long>(type: "INTEGER", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GenerationSnapshots", x => new { x.BoardId, x.GenerationIndex });
                    table.ForeignKey(
                        name: "FK_GenerationSnapshots_Boards_BoardId",
                        column: x => x.BoardId,
                        principalTable: "Boards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BoardOutcomes");

            migrationBuilder.DropTable(
                name: "GenerationSnapshots");

            migrationBuilder.DropTable(
                name: "Boards");
        }
    }
}
