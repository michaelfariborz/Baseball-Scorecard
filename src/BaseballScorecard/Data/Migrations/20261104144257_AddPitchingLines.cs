using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPitchingLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BattersFaced",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EarnedRuns",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Hits",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InningsPitchedOuts",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Runs",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Strikeouts",
                table: "GamePitchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Walks",
                table: "GamePitchers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BattersFaced",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "EarnedRuns",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "Hits",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "InningsPitchedOuts",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "Runs",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "Strikeouts",
                table: "GamePitchers");

            migrationBuilder.DropColumn(
                name: "Walks",
                table: "GamePitchers");
        }
    }
}
