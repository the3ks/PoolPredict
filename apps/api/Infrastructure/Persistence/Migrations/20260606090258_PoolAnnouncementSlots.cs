using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PoolPredict.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PoolAnnouncementSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "announcement_slot",
                table: "pool_messages",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "pool_messages",
                type: "varchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_pool_messages_pool_id_kind_announcement_slot",
                table: "pool_messages",
                columns: new[] { "pool_id", "kind", "announcement_slot" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pool_messages_pool_id_kind_announcement_slot",
                table: "pool_messages");

            migrationBuilder.DropColumn(
                name: "announcement_slot",
                table: "pool_messages");

            migrationBuilder.DropColumn(
                name: "title",
                table: "pool_messages");
        }
    }
}
