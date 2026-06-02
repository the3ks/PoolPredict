using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7ProviderSourceScoping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournaments_external_id",
                table: "tournaments");

            migrationBuilder.DropIndex(
                name: "IX_participants_tournament_id_external_id",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_events_tournament_id_external_id",
                table: "events");

            migrationBuilder.AddColumn<bool>(
                name: "is_test_data",
                table: "tournaments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "tournaments",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Legacy")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_test_data",
                table: "participants",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "participants",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Legacy")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "is_test_data",
                table: "events",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "events",
                type: "varchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Legacy")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_provider_external_id",
                table: "tournaments",
                columns: new[] { "provider", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_tournament_id_provider_external_id",
                table: "participants",
                columns: new[] { "tournament_id", "provider", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_tournament_id_provider_external_id",
                table: "events",
                columns: new[] { "tournament_id", "provider", "external_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_tournaments_provider_external_id",
                table: "tournaments");

            migrationBuilder.DropIndex(
                name: "IX_participants_tournament_id_provider_external_id",
                table: "participants");

            migrationBuilder.DropIndex(
                name: "IX_events_tournament_id_provider_external_id",
                table: "events");

            migrationBuilder.DropColumn(
                name: "is_test_data",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "tournaments");

            migrationBuilder.DropColumn(
                name: "is_test_data",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "participants");

            migrationBuilder.DropColumn(
                name: "is_test_data",
                table: "events");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "events");

            migrationBuilder.CreateIndex(
                name: "IX_tournaments_external_id",
                table: "tournaments",
                column: "external_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participants_tournament_id_external_id",
                table: "participants",
                columns: new[] { "tournament_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_events_tournament_id_external_id",
                table: "events",
                columns: new[] { "tournament_id", "external_id" },
                unique: true);
        }
    }
}
