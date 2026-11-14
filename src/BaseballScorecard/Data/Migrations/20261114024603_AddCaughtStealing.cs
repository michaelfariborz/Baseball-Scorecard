using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaughtStealing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CaughtStealingBase",
                table: "BattingNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CaughtStealingFielders",
                table: "BattingNotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaughtStealingBase",
                table: "BattingNotes");

            migrationBuilder.DropColumn(
                name: "CaughtStealingFielders",
                table: "BattingNotes");
        }
    }
}
