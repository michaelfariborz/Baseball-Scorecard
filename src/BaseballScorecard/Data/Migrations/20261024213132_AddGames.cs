using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace BaseballScorecard.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Games",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    AwayRosterId = table.Column<int>(type: "integer", nullable: false),
                    HomeRosterId = table.Column<int>(type: "integer", nullable: false),
                    Innings = table.Column<int>(type: "integer", nullable: false),
                    Batters = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Games_Rosters_AwayRosterId",
                        column: x => x.AwayRosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Games_Rosters_HomeRosterId",
                        column: x => x.HomeRosterId,
                        principalTable: "Rosters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Games_AwayRosterId",
                table: "Games",
                column: "AwayRosterId");

            migrationBuilder.CreateIndex(
                name: "IX_Games_HomeRosterId",
                table: "Games",
                column: "HomeRosterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Games");
        }
    }
}
