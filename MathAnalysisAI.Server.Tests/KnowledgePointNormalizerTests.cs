using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Data.Seed;
using MathAnalysisAI.Server.Services.Knowledge;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class KnowledgePointNormalizerTests
{
    [Fact]
    public void PlatformSeedData_ShouldHaveUniqueKnowledgePointIdsAndCodes()
    {
        Assert.Equal(
            PlatformSeedData.KnowledgePoints.Length,
            PlatformSeedData.KnowledgePoints.Select(x => x.Id).Distinct().Count());

        Assert.Equal(
            PlatformSeedData.KnowledgePoints.Length,
            PlatformSeedData.KnowledgePoints
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .Select(x => x.Code!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());

        Assert.Equal(
            PlatformSeedData.Chapters.Length,
            PlatformSeedData.Chapters.Select(x => x.Id).Distinct().Count());
    }

    [Fact]
    public async Task NormalizeAsync_ShouldMapNewMathAnalysisTerms()
    {
        var db = TestDb.Create(nameof(NormalizeAsync_ShouldMapNewMathAnalysisTerms));

        var result = await KnowledgePointNormalizer.NormalizeAsync(
            db,
            new[]
            {
                "重积分",
                "二重积分",
                "double integral",
                "三重积分",
                "积分次序",
                "变量替换",
                "极坐标",
                "柱坐标",
                "球坐标",
                "曲线积分",
                "第一类曲线积分",
                "第二类曲线积分",
                "路径无关",
                "保守场",
                "Green 公式",
                "曲面积分",
                "第一类曲面积分",
                "第二类曲面积分",
                "Gauss 公式",
                "Stokes 公式"
            },
            courseId: PlatformSeedData.CourseMathAnalysisId,
            chapterId: 312,
            problemText: "在柱坐标下计算二重积分，并判断曲线积分是否路径无关。",
            studentSolutionText: null);

        Assert.Contains("ma.multiple_integral.concept", result);
        Assert.Contains("ma.multiple_integral.double_integral", result);
        Assert.Contains("ma.multiple_integral.triple_integral", result);
        Assert.Contains("ma.multiple_integral.region_order", result);
        Assert.Contains("ma.multiple_integral.change_of_variables", result);
        Assert.Contains("ma.multiple_integral.coordinate_systems", result);
        Assert.Contains("ma.line_integral.concept", result);
        Assert.Contains("ma.line_integral.scalar", result);
        Assert.Contains("ma.line_integral.vector", result);
        Assert.Contains("ma.line_integral.path_independence", result);
        Assert.Contains("ma.line_integral.green_formula", result);
        Assert.Contains("ma.surface_integral.concept", result);
        Assert.Contains("ma.surface_integral.scalar", result);
        Assert.Contains("ma.surface_integral.flux", result);
        Assert.Contains("ma.surface_integral.gauss_formula", result);
        Assert.Contains("ma.surface_integral.stokes_formula", result);
    }

    [Fact]
    public async Task NormalizeAsync_ShouldMapMisconceptionTermsAndKeepImproperIntegralFallback()
    {
        var db = TestDb.Create(nameof(NormalizeAsync_ShouldMapMisconceptionTermsAndKeepImproperIntegralFallback));

        var result = await KnowledgePointNormalizer.NormalizeAsync(
            db,
            new[]
            {
                "一致收敛",
                "函数项级数一致收敛",
                "逐点收敛",
                "逐点收敛与一致收敛区分",
                "幂级数端点",
                "泰勒余项",
                "瑕点",
                "奇点",
                "中值定理条件",
                "极限与积分交换",
                "endpoint convergence",
                "remainder term",
                "improper integral singularity"
            },
            courseId: PlatformSeedData.CourseMathAnalysisId,
            chapterId: 307,
            problemText: "讨论反常积分在瑕点处的收敛性。",
            studentSolutionText: null);

        Assert.Contains("ma.function_series.uniform_convergence", result);
        Assert.Contains("ma.function_series.uniform_convergence.criteria", result);
        Assert.Contains("ma.function_series.pointwise_convergence", result);
        Assert.Contains("ma.function_series.pointwise_vs_uniform", result);
        Assert.Contains("ma.power_series.endpoint_convergence", result);
        Assert.Contains("ma.power_series.taylor_remainder", result);
        Assert.Contains("ma.improper_integral.singularity_split", result);
        Assert.Contains("ma.mean_value_theorem.conditions_check", result);
        Assert.Contains("ma.function_series.limit_exchange_conditions", result);
    }

    [Fact]
    public async Task NormalizeAsync_ShouldKeepImproperIntegralFallbackWorking()
    {
        var db = TestDb.Create(nameof(NormalizeAsync_ShouldKeepImproperIntegralFallbackWorking));

        var result = await KnowledgePointNormalizer.NormalizeAsync(
            db,
            new[] { "反常积分" },
            courseId: PlatformSeedData.CourseMathAnalysisId,
            chapterId: 307,
            problemText: "讨论反常积分的收敛性。",
            studentSolutionText: null);

        Assert.Contains("ma.improper_integral.convergence_criteria", result);
    }

}
