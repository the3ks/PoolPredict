using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PredictionCancellation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "predictions",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Active")
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "status",
                table: "predictions");
        }
    }
}
