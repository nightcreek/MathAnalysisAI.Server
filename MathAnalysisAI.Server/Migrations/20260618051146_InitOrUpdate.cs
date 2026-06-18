using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MathAnalysisAI.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitOrUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TeacherId",
                table: "AppUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "NetworkResources",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CourseId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    Link = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NetworkResources", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NetworkResources_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "NetworkResources",
                columns: new[] { "Id", "Category", "CourseId", "CreatedAt", "Description", "IsEnabled", "Link", "SortOrder", "Title", "UpdatedAt" },
                values: new object[,]
                {
                    { 5001, "教材与参考书", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "国内经典数学分析教材，适合本科基础阶段系统学习。", true, "https://book.douban.com/subject/26802081/", 1, "华东师范大学 · 数学分析（第四版）", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5002, "教材与参考书", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "从自然数出发的现代分析入门，强调直觉与严格性的结合。", true, "https://terrytao.wordpress.com/books/analysis-i/", 2, "陶哲轩 · Analysis I / Analysis II", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5003, "在线课程", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "MIT OpenCourseWare 上的实分析公开课，含完整视频与习题。", true, "https://ocw.mit.edu/courses/18-100a-real-analysis-fall-2020/", 3, "MIT 18.100 Real Analysis (OCW)", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5004, "在线课程", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "直观视频讲解微积分核心概念，作为先修或复习非常合适。", true, "https://www.khanacademy.org/math/calculus-1", 4, "可汗学院 · Calculus", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5005, "交互式工具", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "在线图形计算器，可输入函数直观查看收敛、连续性、极值等分析行为。", true, "https://www.desmos.com/calculator", 5, "Desmos Graphing Calculator", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5006, "交互式工具", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "权威的在线数学百科，定义、定理与例子可以作为参考书补充。", true, "https://mathworld.wolfram.com/", 6, "Wolfram MathWorld", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5007, "习题与讨论", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "遇到具体题目或概念困惑时，可以在这里搜索类似问题或提问。", true, "https://math.stackexchange.com/", 7, "Math Stack Exchange", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5008, "符号计算", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "免费的 Python 符号计算库，可以辅助推导极限、积分与级数。", true, "https://www.sympy.org/", 8, "SymPy - Python Symbolic Mathematics", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 5009, "中文开放课程", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "国内多所高校在 MOOC 平台开设的数学分析公开课程，适合中文环境学习。", true, "https://www.icourse163.org/search.htm?search=%E6%95%B0%E5%AD%A6%E5%88%86%E6%9E%90", 9, "中国大学 MOOC · 数学分析", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppUsers_TeacherId",
                table: "AppUsers",
                column: "TeacherId");

            migrationBuilder.CreateIndex(
                name: "IX_NetworkResources_CourseId_IsEnabled",
                table: "NetworkResources",
                columns: new[] { "CourseId", "IsEnabled" });

            migrationBuilder.AddForeignKey(
                name: "FK_AppUsers_AppUsers_TeacherId",
                table: "AppUsers",
                column: "TeacherId",
                principalTable: "AppUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppUsers_AppUsers_TeacherId",
                table: "AppUsers");

            migrationBuilder.DropTable(
                name: "NetworkResources");

            migrationBuilder.DropIndex(
                name: "IX_AppUsers_TeacherId",
                table: "AppUsers");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "AppUsers");
        }
    }
}
