using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    public partial class AddAnalysisResultReliabilityFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnswerReliability",
                table: "AnalysisResults",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Uncertain");

            migrationBuilder.AddColumn<bool>(
                name: "NeedsReview",
                table: "AnalysisResults",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ReliabilityReasonsJson",
                table: "AnalysisResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifierWarningsJson",
                table: "AnalysisResults",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "AnalysisResults",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnswerReliability",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "NeedsReview",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "ReliabilityReasonsJson",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "VerifierWarningsJson",
                table: "AnalysisResults");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "AnalysisResults");
        }
    }
}
