using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    public partial class AddStructuredProblems : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StructuredProblemId",
                table: "Problems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StructuredProblemId",
                table: "AnalysisResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "StructuredProblems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    RawProblemText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NormalizedProblemText = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentSolutionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormulasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    GivenConditionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProblemType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    KnowledgePointCandidatesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PhotoSolutionOcrRecordId = table.Column<int>(type: "int", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructuredProblems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StructuredProblems_AppUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StructuredProblems_PhotoSolutionOcrRecords_PhotoSolutionOcrRecordId",
                        column: x => x.PhotoSolutionOcrRecordId,
                        principalTable: "PhotoSolutionOcrRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Problems_StructuredProblemId",
                table: "Problems",
                column: "StructuredProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_StructuredProblemId",
                table: "AnalysisResults",
                column: "StructuredProblemId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredProblems_CreatedByUserId",
                table: "StructuredProblems",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredProblems_PhotoSolutionOcrRecordId",
                table: "StructuredProblems",
                column: "PhotoSolutionOcrRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_StructuredProblems_Status",
                table: "StructuredProblems",
                column: "Status");

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisResults_StructuredProblems_StructuredProblemId",
                table: "AnalysisResults",
                column: "StructuredProblemId",
                principalTable: "StructuredProblems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_StructuredProblems_StructuredProblemId",
                table: "Problems",
                column: "StructuredProblemId",
                principalTable: "StructuredProblems",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisResults_StructuredProblems_StructuredProblemId",
                table: "AnalysisResults");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_StructuredProblems_StructuredProblemId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_Problems_StructuredProblemId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisResults_StructuredProblemId",
                table: "AnalysisResults");

            migrationBuilder.DropTable(
                name: "StructuredProblems");

            migrationBuilder.DropColumn(
                name: "StructuredProblemId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "StructuredProblemId",
                table: "AnalysisResults");
        }
    }
}
