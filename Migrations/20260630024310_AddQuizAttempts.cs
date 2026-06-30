using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDHub.Migrations
{
    /// <inheritdoc />
    public partial class AddQuizAttempts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuizAttempts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    ModuleId = table.Column<int>(type: "int", nullable: false),
                    QuizId = table.Column<int>(type: "int", nullable: false),
                    TrainingId = table.Column<int>(type: "int", nullable: false),
                    CorrectCount = table.Column<int>(type: "int", nullable: false),
                    TotalQuestions = table.Column<int>(type: "int", nullable: false),
                    ScorePercentage = table.Column<int>(type: "int", nullable: false),
                    IsPassed = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    TakenAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuizAttempts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_quiz_QuizId",
                        column: x => x.QuizId,
                        principalTable: "quiz",
                        principalColumn: "quiz_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_training_modules_ModuleId",
                        column: x => x.ModuleId,
                        principalTable: "training_modules",
                        principalColumn: "module_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_trainings_TrainingId",
                        column: x => x.TrainingId,
                        principalTable: "trainings",
                        principalColumn: "training_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_QuizAttempts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_ModuleId",
                table: "QuizAttempts",
                column: "ModuleId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_QuizId",
                table: "QuizAttempts",
                column: "QuizId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_TrainingId",
                table: "QuizAttempts",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_QuizAttempts_UserId",
                table: "QuizAttempts",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuizAttempts");
        }
    }
}
