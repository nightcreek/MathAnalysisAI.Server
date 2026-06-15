using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyWrongQuestionsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WrongQuestions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WrongQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CleanLatex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ErrorAnalysis = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImagePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ImprovementSuggestion = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    KnowledgePoint = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OverallEvaluation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawLatex = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StandardSolution = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StudentAnswer = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WrongQuestions", x => x.Id);
                });
        }
    }
}
