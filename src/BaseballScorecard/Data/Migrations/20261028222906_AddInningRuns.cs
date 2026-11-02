using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInningRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InningRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    RosterId = table.Column<int>(type: "integer", nullable: false),
                    Inning = table.Column<int>(type: "integer", nullable: false),
                    Runs = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InningRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InningRuns_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InningRuns_Rosters_RosterId",
                        column: x => x.RosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InningRuns_GameId_RosterId_Inning",
                table: "InningRuns",
                columns: new[] { "GameId", "RosterId", "Inning" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InningRuns_RosterId",
                table: "InningRuns",
                column: "RosterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InningRuns");
        }
    }
}
