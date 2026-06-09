using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    public partial class AddLlmRequestLogAttemptTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AttemptCount",
                table: "LLMRequestLogs",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "StatusCode",
                table: "LLMRequestLogs",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "LLMRequestLogs",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ErrorMessage",
                table: "LLMRequestLogs",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2048)",
                oldMaxLength: 2048,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "AttemptCount",
                table: "LLMRequestLogs");

            migrationBuilder.DropColumn(
                name: "StatusCode",
                table: "LLMRequestLogs");
        }
    }
}
