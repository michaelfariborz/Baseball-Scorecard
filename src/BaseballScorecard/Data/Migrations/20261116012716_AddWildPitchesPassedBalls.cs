using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWildPitchesPassedBalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "PassedBallBases",
                table: "BattingNotes",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);

            migrationBuilder.AddColumn<int[]>(
                name: "WildPitchBases",
                table: "BattingNotes",
                type: "integer[]",
                nullable: false,
                defaultValue: new int[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PassedBallBases",
                table: "BattingNotes");

            migrationBuilder.DropColumn(
                name: "WildPitchBases",
                table: "BattingNotes");
        }
    }
}
