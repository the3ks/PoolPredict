using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7EventResultsSettlement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "event_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    event_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    full_time_home_score = table.Column<int>(type: "int", nullable: false),
                    full_time_away_score = table.Column<int>(type: "int", nullable: false),
                    first_half_home_score = table.Column<int>(type: "int", nullable: true),
                    first_half_away_score = table.Column<int>(type: "int", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_results", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_event_results_event_id",
                table: "event_results",
                column: "event_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "event_results");
        }
    }
}
