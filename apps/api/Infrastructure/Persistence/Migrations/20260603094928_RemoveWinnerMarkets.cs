using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWinnerMarkets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DELETE FROM point_ledger
                WHERE prediction_id IN (
                    SELECT id FROM predictions WHERE market_type = 'Winner'
                );
                """);

            migrationBuilder.Sql("""
                DELETE FROM settlement_logs
                WHERE prediction_id IN (
                    SELECT id FROM predictions WHERE market_type = 'Winner'
                );
                """);

            migrationBuilder.Sql("DELETE FROM predictions WHERE market_type = 'Winner';");
            migrationBuilder.Sql("DELETE FROM markets WHERE type = 'Winner';");
            migrationBuilder.Sql("DELETE FROM payout_configuration_market_rules WHERE market_type = 'Winner';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Winner markets are intentionally removed from the platform; deleted user predictions are not recreated.
        }
    }
}
