using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MathAnalysisAI.Server.Migrations
{
    public partial class AddMathAnalysisKnowledgePointSeeds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Chapters",
                columns: new[] { "Id", "Code", "CourseId", "CreatedAt", "Description", "Name", "OrderIndex" },
                values: new object[,]
                {
                    { 312, "ma.multiple_integral", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "二重积分与三重积分", "重积分", 12 },
                    { 313, "ma.line_integral", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "第一类与第二类曲线积分", "曲线积分", 13 },
                    { 314, "ma.surface_integral", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "第一类与第二类曲面积分", "曲面积分", 14 }
                });

            migrationBuilder.InsertData(
                table: "KnowledgePoints",
                columns: new[] { "Id", "ChapterId", "Code", "CourseId", "CreatedAt", "Description", "Name" },
                values: new object[,]
                {
                    { 1045, 312, "ma.multiple_integral.concept", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "重积分定义与基本概念", "重积分概念" },
                    { 1046, 312, "ma.multiple_integral.double_integral", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "二重积分的定义与计算", "二重积分定义与计算" },
                    { 1047, 312, "ma.multiple_integral.triple_integral", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "三重积分的定义与计算", "三重积分定义与计算" },
                    { 1048, 312, "ma.multiple_integral.region_order", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "积分区域描述与积分次序交换", "积分区域与积分次序" },
                    { 1049, 312, "ma.multiple_integral.change_of_variables", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "重积分换元与雅可比", "重积分变量替换" },
                    { 1050, 312, "ma.multiple_integral.coordinate_systems", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "极坐标、柱坐标与球坐标下的重积分", "坐标系下的重积分" },
                    { 1051, 313, "ma.line_integral.concept", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "曲线积分基本概念", "曲线积分概念" },
                    { 1052, 313, "ma.line_integral.scalar", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "标量场上的曲线积分", "第一类曲线积分" },
                    { 1053, 313, "ma.line_integral.vector", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "向量场上的曲线积分", "第二类曲线积分" },
                    { 1054, 313, "ma.line_integral.path_independence", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "路径无关性、保守场与势函数", "路径无关性与保守场" },
                    { 1055, 313, "ma.line_integral.green_formula", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Green 公式与平面区域积分", "Green 公式" },
                    { 1056, 314, "ma.surface_integral.concept", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "曲面积分基本概念", "曲面积分概念" },
                    { 1057, 314, "ma.surface_integral.scalar", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "标量场上的曲面积分", "第一类曲面积分" },
                    { 1058, 314, "ma.surface_integral.flux", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "通量型曲面积分", "第二类曲面积分" },
                    { 1059, 314, "ma.surface_integral.gauss_formula", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Gauss 散度公式", "Gauss 公式" },
                    { 1060, 314, "ma.surface_integral.stokes_formula", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Stokes 公式", "Stokes 公式" },
                    { 1061, 307, "ma.improper_integral.singularity_split", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "反常积分遇到瑕点时的拆分处理", "反常积分瑕点拆分" },
                    { 1062, 310, "ma.power_series.endpoint_convergence", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "幂级数端点的收敛性判定", "幂级数端点收敛" },
                    { 1063, 309, "ma.function_series.uniform_convergence.criteria", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "函数项级数一致收敛与常见判定", "函数项级数一致收敛" },
                    { 1064, 309, "ma.function_series.pointwise_vs_uniform", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "逐点收敛与一致收敛的区别", "逐点收敛与一致收敛区分" },
                    { 1065, 310, "ma.power_series.taylor_remainder", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "泰勒公式余项与截断误差", "泰勒公式余项" },
                    { 1066, 304, "ma.mean_value_theorem.conditions_check", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "使用中值定理前的条件检查", "中值定理条件检查" },
                    { 1067, 309, "ma.function_series.limit_exchange_conditions", 200, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "极限与积分交换的条件", "极限与积分交换条件" }
                });

            migrationBuilder.InsertData(
                table: "KnowledgeDependencies",
                columns: new[] { "Id", "CreatedAt", "DependencyType", "FromKnowledgePointId", "ToKnowledgePointId" },
                values: new object[,]
                {
                    { 2013, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1021, 1045 },
                    { 2014, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1042, 1045 },
                    { 2015, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1042, 1051 },
                    { 2016, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1042, 1056 },
                    { 2017, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1034, 1063 },
                    { 2018, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1034, 1064 },
                    { 2019, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1040, 1065 },
                    { 2020, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1014, 1066 },
                    { 2021, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "prerequisite", 1034, 1067 }
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2013);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2014);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2015);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2016);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2017);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2018);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2019);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2020);
            migrationBuilder.DeleteData(table: "KnowledgeDependencies", keyColumn: "Id", keyValue: 2021);

            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1045);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1046);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1047);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1048);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1049);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1050);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1051);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1052);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1053);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1054);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1055);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1056);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1057);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1058);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1059);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1060);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1061);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1062);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1063);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1064);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1065);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1066);
            migrationBuilder.DeleteData(table: "KnowledgePoints", keyColumn: "Id", keyValue: 1067);

            migrationBuilder.DeleteData(table: "Chapters", keyColumn: "Id", keyValue: 312);
            migrationBuilder.DeleteData(table: "Chapters", keyColumn: "Id", keyValue: 313);
            migrationBuilder.DeleteData(table: "Chapters", keyColumn: "Id", keyValue: 314);
        }
    }
}
