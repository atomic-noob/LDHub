using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LDHub.Migrations
{
    /// <inheritdoc />
    public partial class AddNewTrainingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        

            migrationBuilder.AddColumn<string>(
                name: "MOVs",
                table: "trainings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "development_act",
                table: "trainings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "proficiency_id",
                table: "trainings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "region_id",
                table: "trainings",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "success_indicator",
                table: "trainings",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "training_code",
                table: "trainings",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "training_hrs",
                table: "trainings",
                type: "int",
                nullable: true);

          
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "in_training",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_external",
                table: "users");

            migrationBuilder.DropColumn(
                name: "MOVs",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "development_act",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "proficiency_id",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "region_id",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "success_indicator",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "training_code",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "training_hrs",
                table: "trainings");

            migrationBuilder.RenameColumn(
                name: "training_title",
                table: "trainings",
                newName: "title");

            migrationBuilder.AlterColumn<int>(
                name: "created_by",
                table: "trainings",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status",
                table: "trainings",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "training_assignments",
                columns: table => new
                {
                    assignment_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    training_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    accepted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    assigned_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    completed_at = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_assignments", x => x.assignment_id);
                    table.ForeignKey(
                        name: "FK_training_assignments_trainings_training_id",
                        column: x => x.training_id,
                        principalTable: "trainings",
                        principalColumn: "training_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_assignments_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "training_modules",
                columns: table => new
                {
                    module_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    training_id = table.Column<int>(type: "int", nullable: false),
                    uploaded_by = table.Column<int>(type: "int", nullable: false),
                    content = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    file_path = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lecture_source = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    link_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module_description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module_name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    module_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_training_modules", x => x.module_id);
                    table.ForeignKey(
                        name: "FK_training_modules_trainings_training_id",
                        column: x => x.training_id,
                        principalTable: "trainings",
                        principalColumn: "training_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_training_modules_users_uploaded_by",
                        column: x => x.uploaded_by,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_trainings_created_by",
                table: "trainings",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "IX_training_assignments_training_id",
                table: "training_assignments",
                column: "training_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_assignments_user_id",
                table: "training_assignments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_modules_training_id",
                table: "training_modules",
                column: "training_id");

            migrationBuilder.CreateIndex(
                name: "IX_training_modules_uploaded_by",
                table: "training_modules",
                column: "uploaded_by");

            migrationBuilder.AddForeignKey(
                name: "FK_trainings_users_created_by",
                table: "trainings",
                column: "created_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
