using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoSolutionOcrRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedPracticeProblems_AppUsers_UserId",
                table: "GeneratedPracticeProblems");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeAttempts_AppUsers_UserId",
                table: "PracticeAttempts");

            migrationBuilder.AddColumn<int>(
                name: "PhotoSolutionOcrRecordId",
                table: "Problems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PhotoSolutionOcrRecordId",
                table: "AnalysisResults",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoSolutionOcrRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: false),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    ChapterId = table.Column<int>(type: "int", nullable: true),
                    OriginalFileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ImageHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    OcrProvider = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OcrModelName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RecognizedProblemText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RecognizedStudentSolutionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DetectedSectionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FormulasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WarningsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReviewReasonsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Confidence = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    ConfirmedProblemText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedStudentSolutionText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedFormulasJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ConfirmedByUserId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoSolutionOcrRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PhotoSolutionOcrRecords_AppUsers_ConfirmedByUserId",
                        column: x => x.ConfirmedByUserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PhotoSolutionOcrRecords_AppUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AppUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Problems_PhotoSolutionOcrRecordId",
                table: "Problems",
                column: "PhotoSolutionOcrRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_AnalysisResults_PhotoSolutionOcrRecordId",
                table: "AnalysisResults",
                column: "PhotoSolutionOcrRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSolutionOcrRecords_ConfirmedByUserId",
                table: "PhotoSolutionOcrRecords",
                column: "ConfirmedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSolutionOcrRecords_ImageHash",
                table: "PhotoSolutionOcrRecords",
                column: "ImageHash");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSolutionOcrRecords_UserId_Status",
                table: "PhotoSolutionOcrRecords",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PhotoSolutionOcrRecords_UserId_UploadedAt",
                table: "PhotoSolutionOcrRecords",
                columns: new[] { "UserId", "UploadedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_AnalysisResults_PhotoSolutionOcrRecords_PhotoSolutionOcrRecordId",
                table: "AnalysisResults",
                column: "PhotoSolutionOcrRecordId",
                principalTable: "PhotoSolutionOcrRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedPracticeProblems_AppUsers_UserId",
                table: "GeneratedPracticeProblems",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeAttempts_AppUsers_UserId",
                table: "PracticeAttempts",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_PhotoSolutionOcrRecords_PhotoSolutionOcrRecordId",
                table: "Problems",
                column: "PhotoSolutionOcrRecordId",
                principalTable: "PhotoSolutionOcrRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AnalysisResults_PhotoSolutionOcrRecords_PhotoSolutionOcrRecordId",
                table: "AnalysisResults");

            migrationBuilder.DropForeignKey(
                name: "FK_GeneratedPracticeProblems_AppUsers_UserId",
                table: "GeneratedPracticeProblems");

            migrationBuilder.DropForeignKey(
                name: "FK_PracticeAttempts_AppUsers_UserId",
                table: "PracticeAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_PhotoSolutionOcrRecords_PhotoSolutionOcrRecordId",
                table: "Problems");

            migrationBuilder.DropTable(
                name: "PhotoSolutionOcrRecords");

            migrationBuilder.DropIndex(
                name: "IX_Problems_PhotoSolutionOcrRecordId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_AnalysisResults_PhotoSolutionOcrRecordId",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "PhotoSolutionOcrRecordId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "PhotoSolutionOcrRecordId",
                table: "AnalysisResults");

            migrationBuilder.AddForeignKey(
                name: "FK_GeneratedPracticeProblems_AppUsers_UserId",
                table: "GeneratedPracticeProblems",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_PracticeAttempts_AppUsers_UserId",
                table: "PracticeAttempts",
                column: "UserId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
