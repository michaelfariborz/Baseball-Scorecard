using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddInningColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BattingNotes_GameId_RosterId_BattingOrder_Inning",
                table: "BattingNotes");

            migrationBuilder.AddColumn<int>(
                name: "Column",
                table: "BattingNotes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_BattingNotes_GameId_RosterId_BattingOrder_Inning_Column",
                table: "BattingNotes",
                columns: new[] { "GameId", "RosterId", "BattingOrder", "Inning", "Column" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BattingNotes_GameId_RosterId_BattingOrder_Inning_Column",
                table: "BattingNotes");

            migrationBuilder.DropColumn(
                name: "Column",
                table: "BattingNotes");

            migrationBuilder.CreateIndex(
                name: "IX_BattingNotes_GameId_RosterId_BattingOrder_Inning",
                table: "BattingNotes",
                columns: new[] { "GameId", "RosterId", "BattingOrder", "Inning" },
                unique: true);
        }
    }
}
