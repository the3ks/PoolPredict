using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PoolMemberLeaderboardStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "leaderboard_status",
                table: "pool_members",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Ranked");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "leaderboard_status",
                table: "pool_members");
        }
    }
}
