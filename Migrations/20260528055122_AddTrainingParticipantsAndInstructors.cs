using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDHub.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingParticipantsAndInstructors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "training_instructors",
                columns: table => new
                {
                    training_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    assigned_date = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_instructors", x => new { x.training_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_training_instructors_trainings_training_id",
                        column: x => x.training_id,
                        principalTable: "trainings",
                        principalColumn: "training_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_instructors_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "training_participants",
                columns: table => new
                {
                    training_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    enrollment_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completion_status = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_participants", x => new { x.training_id, x.user_id });
                    table.ForeignKey(
                        name: "FK_training_participants_trainings_training_id",
                        column: x => x.training_id,
                        principalTable: "trainings",
                        principalColumn: "training_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_training_instructors_user_id",
                table: "training_instructors",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_participants_user_id",
                table: "training_participants",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "training_instructors");

            migrationBuilder.DropTable(
                name: "training_participants");
        }
    }
}
