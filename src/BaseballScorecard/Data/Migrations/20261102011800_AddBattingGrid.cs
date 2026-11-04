using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBattingGrid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattingNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    RosterId = table.Column<int>(type: "integer", nullable: false),
                    BattingOrder = table.Column<int>(type: "integer", nullable: false),
                    Inning = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattingNotes_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BattingNotes_Rosters_RosterId",
                        column: x => x.RosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InningHitsLob",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GameId = table.Column<int>(type: "integer", nullable: false),
                    RosterId = table.Column<int>(type: "integer", nullable: false),
                    Inning = table.Column<int>(type: "integer", nullable: false),
                    Hits = table.Column<int>(type: "integer", nullable: true),
                    Lob = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InningHitsLob", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InningHitsLob_Games_GameId",
                        column: x => x.GameId,
                        principalTable: "Games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InningHitsLob_Rosters_RosterId",
                        column: x => x.RosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattingNotes_GameId_RosterId_BattingOrder_Inning",
                table: "BattingNotes",
                columns: new[] { "GameId", "RosterId", "BattingOrder", "Inning" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BattingNotes_RosterId",
                table: "BattingNotes",
                column: "RosterId");

            migrationBuilder.CreateIndex(
                name: "IX_InningHitsLob_GameId_RosterId_Inning",
                table: "InningHitsLob",
                columns: new[] { "GameId", "RosterId", "Inning" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InningHitsLob_RosterId",
                table: "InningHitsLob",
                column: "RosterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattingNotes");

            migrationBuilder.DropTable(
                name: "InningHitsLob");
        }
    }
}
