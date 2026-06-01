using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint5MarketSettlementFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "payout_configurations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    version = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_configurations", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "settlement_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    event_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    status = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    started_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_runs", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payout_configuration_market_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    payout_configuration_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    profile = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    market_type = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    period = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    line_value = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    payout_multiplier = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    is_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payout_configuration_market_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_payout_configuration_market_rules_payout_configurations_payo~",
                        column: x => x.payout_configuration_id,
                        principalTable: "payout_configurations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "settlement_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    settlement_run_id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    prediction_id = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    level = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_settlement_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_settlement_logs_settlement_runs_settlement_run_id",
                        column: x => x.settlement_run_id,
                        principalTable: "settlement_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payout_configuration_market_rules_payout_configuration_id_pr~",
                table: "payout_configuration_market_rules",
                columns: new[] { "payout_configuration_id", "profile", "market_type", "period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payout_configurations_version",
                table: "payout_configurations",
                column: "version",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_settlement_logs_settlement_run_id",
                table: "settlement_logs",
                column: "settlement_run_id");

            migrationBuilder.CreateIndex(
                name: "IX_settlement_runs_event_id",
                table: "settlement_runs",
                column: "event_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "payout_configuration_market_rules");

            migrationBuilder.DropTable(
                name: "settlement_logs");

            migrationBuilder.DropTable(
                name: "payout_configurations");

            migrationBuilder.DropTable(
                name: "settlement_runs");
        }
    }
}
