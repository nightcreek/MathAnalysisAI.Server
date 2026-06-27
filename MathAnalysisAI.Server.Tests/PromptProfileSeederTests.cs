using MathAnalysisAI.Server.Data.Seed;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.LLM;
using MathAnalysisAI.Server.Services.Analysis.UAO;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class PromptProfileSeederTests
{
    [Fact]
    public async Task SeedMathAnalysisPromptProfilesAsync_ShouldSeedBoundaryAwarePromptProfiles()
    {
        var db = TestDb.Create(nameof(SeedMathAnalysisPromptProfilesAsync_ShouldSeedBoundaryAwarePromptProfiles));

        var inserted = await PromptProfileSeeder.SeedMathAnalysisPromptProfilesAsync(db);

        Assert.Equal(5, inserted);

        var reviewProfile = db.PromptProfiles.Single(x => x.CourseId == PlatformSeedData.CourseMathAnalysisId && x.Mode == "review_solution");
        Assert.Equal("v4", reviewProfile.Version);
        Assert.Contains("数学分析课程学习智能体", reviewProfile.SystemPrompt);
        Assert.Contains("可能超出当前数学分析课程范围", reviewProfile.SystemPrompt);
        Assert.Contains("定义域", reviewProfile.SystemPrompt);
        Assert.Contains("一致收敛条件", reviewProfile.SystemPrompt);
        Assert.Contains("路径无关 vs 曲线积分为 0", reviewProfile.SystemPrompt);
        Assert.Contains("请先识别章节、知识点和题型", reviewProfile.UserPromptTemplate);
    }

    [Fact]
    public async Task LlmRequestFactory_ShouldUseStrengthenedPromptProfileAndFallbackBoundaryPrompt()
    {
        var db = TestDb.Create(nameof(LlmRequestFactory_ShouldUseStrengthenedPromptProfileAndFallbackBoundaryPrompt));
        await PromptProfileSeeder.SeedMathAnalysisPromptProfilesAsync(db);

        var factory = new LlmRequestFactory(db);
        var request = await factory.BuildAsync(
            new UAOInputModel
            {
                CourseId = PlatformSeedData.CourseMathAnalysisId,
                AnalysisMode = "review_solution",
                ProblemText = "判断积分是否收敛",
                StudentSolutionText = "我认为收敛",
                UserId = 7
            },
            new Course
            {
                Id = PlatformSeedData.CourseMathAnalysisId,
                Name = "数学分析"
            },
            new Chapter
            {
                Id = 307,
                CourseId = PlatformSeedData.CourseMathAnalysisId,
                Name = "反常积分",
                Code = "ma.improper_integral"
            },
            new Problem
            {
                Id = 1,
                CourseId = PlatformSeedData.CourseMathAnalysisId,
                ContentMarkdown = "判断积分是否收敛"
            },
            new StudentSolution
            {
                Id = 1,
                ProblemId = 1,
                SolutionText = "我认为收敛"
            },
            AnalysisContextDto.Empty,
            analysisResultId: 99,
            CancellationToken.None);

        Assert.Contains("数学分析课程学习智能体", request.Messages[0].Content);
        Assert.Contains("可能超出当前数学分析课程范围", request.Messages[0].Content);
        Assert.Contains("定义域", request.Messages[0].Content);
        Assert.Contains("请先识别章节、知识点和题型", request.Messages[1].Content);
        Assert.Contains("\"knowledgeContext\":[]", request.Messages[1].Content.Replace(" ", string.Empty));
        Assert.Contains("review_solution", request.RequestType);
    }
}
