using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint7EventManagementMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "management_mode",
                table: "events",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Provider")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "management_mode",
                table: "events");
        }
    }
}
