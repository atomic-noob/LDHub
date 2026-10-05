using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDHub.Migrations
{
    /// <inheritdoc />
    public partial class AddCertificateToTraining : Migration
    {
        /// <inheritdoc />
       protected override void Up(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.AddColumn<string>(
                    name: "certificate_path",
                    table: "trainings",
                    type: "varchar(500)",
                    maxLength: 500,
                    nullable: true);
            }

            protected override void Down(MigrationBuilder migrationBuilder)
            {
                migrationBuilder.DropColumn(
                    name: "certificate_path",
                    table: "trainings");
            }
    }
}
