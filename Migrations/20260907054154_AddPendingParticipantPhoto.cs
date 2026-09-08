using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDHub.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingParticipantPhoto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhotoPath",
                table: "pending_participants",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhotoPath",
                table: "pending_participants");
        }
    }
}
