using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.Data.Seed;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Knowledge;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class KnowledgeRetrievalServiceTests
{
    [Fact]
    public async Task RetrieveAsync_ShouldRecallMultipleIntegralChunk()
    {
        var db = TestDb.Create(nameof(RetrieveAsync_ShouldRecallMultipleIntegralChunk));
        SeedChunk(db,
            chunkId: 9001,
            materialId: 8001,
            chapterId: 312,
            title: "重积分讲义",
            sectionTitle: "二重积分与积分次序",
            sectionPath: "重积分/二重积分/积分次序",
            preview: "二重积分改变积分次序并使用变量替换。",
            knowledgePointCodes: new[]
            {
                "ma.multiple_integral.double_integral",
                "ma.multiple_integral.region_order",
                "ma.multiple_integral.change_of_variables"
            });

        var service = new KnowledgeRetrievalService(db);
        var result = await service.RetrieveAsync(new()
        {
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ProblemText = "二重积分改变积分次序",
            TopK = 3
        });

        var chunk = Assert.Single(result, x => x.Title == "重积分讲义");
        Assert.Contains("二重积分定义与计算", chunk.MatchedKnowledgePoints);
        Assert.Contains("积分区域与积分次序", chunk.MatchedKnowledgePoints);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldRecallLineIntegralChunk()
    {
        var db = TestDb.Create(nameof(RetrieveAsync_ShouldRecallLineIntegralChunk));
        SeedChunk(db,
            chunkId: 9002,
            materialId: 8002,
            chapterId: 313,
            title: "曲线积分讲义",
            sectionTitle: "第二类曲线积分与路径无关",
            sectionPath: "曲线积分/第二类曲线积分",
            preview: "第二类曲线积分在保守场中与路径无关。",
            knowledgePointCodes: new[]
            {
                "ma.line_integral.vector",
                "ma.line_integral.path_independence"
            });

        var service = new KnowledgeRetrievalService(db);
        var result = await service.RetrieveAsync(new()
        {
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ProblemText = "第二类曲线积分与路径无关",
            TopK = 3
        });

        var chunk = Assert.Single(result, x => x.Title == "曲线积分讲义");
        Assert.Contains("第二类曲线积分", chunk.MatchedKnowledgePoints);
        Assert.Contains("路径无关性与保守场", chunk.MatchedKnowledgePoints);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldRecallSurfaceIntegralChunk()
    {
        var db = TestDb.Create(nameof(RetrieveAsync_ShouldRecallSurfaceIntegralChunk));
        SeedChunk(db,
            chunkId: 9003,
            materialId: 8003,
            chapterId: 314,
            title: "曲面积分讲义",
            sectionTitle: "Gauss 公式与 Stokes 公式",
            sectionPath: "曲面积分/Gauss/Stokes",
            preview: "曲面积分可用 Gauss 公式和 Stokes 公式讨论通量。",
            knowledgePointCodes: new[]
            {
                "ma.surface_integral.concept",
                "ma.surface_integral.gauss_formula",
                "ma.surface_integral.stokes_formula"
            });

        var service = new KnowledgeRetrievalService(db);
        var result = await service.RetrieveAsync(new()
        {
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ProblemText = "曲面积分 Gauss 公式 Stokes 公式",
            TopK = 3
        });

        var chunk = Assert.Single(result, x => x.Title == "曲面积分讲义");
        Assert.Contains("曲面积分概念", chunk.MatchedKnowledgePoints);
        Assert.Contains("Gauss 公式", chunk.MatchedKnowledgePoints);
        Assert.Contains("Stokes 公式", chunk.MatchedKnowledgePoints);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldRecallConvergenceAndPowerSeriesChunks()
    {
        var db = TestDb.Create(nameof(RetrieveAsync_ShouldRecallConvergenceAndPowerSeriesChunks));
        SeedChunk(db,
            chunkId: 9004,
            materialId: 8004,
            chapterId: 309,
            title: "函数项级数讲义",
            sectionTitle: "一致收敛与逐点收敛",
            sectionPath: "函数项级数/一致收敛",
            preview: "一致收敛与逐点收敛的区别，以及极限与积分交换条件。",
            knowledgePointCodes: new[]
            {
                "ma.function_series.uniform_convergence",
                "ma.function_series.pointwise_convergence",
                "ma.function_series.limit_exchange_conditions"
            });

        SeedChunk(db,
            chunkId: 9005,
            materialId: 8005,
            chapterId: 310,
            title: "幂级数讲义",
            sectionTitle: "幂级数端点收敛",
            sectionPath: "幂级数/端点",
            preview: "讨论幂级数端点收敛与泰勒公式余项。",
            knowledgePointCodes: new[]
            {
                "ma.power_series.endpoint_convergence",
                "ma.power_series.taylor_remainder"
            });

        var service = new KnowledgeRetrievalService(db);
        var result = await service.RetrieveAsync(new()
        {
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ProblemText = "一致收敛 逐点收敛 幂级数端点",
            TopK = 5
        });

        var seriesChunk = Assert.Single(result, x => x.Title == "函数项级数讲义");
        Assert.Contains("一致收敛定义", seriesChunk.MatchedKnowledgePoints);
        Assert.Contains("函数列逐点收敛", seriesChunk.MatchedKnowledgePoints);

        var powerChunk = Assert.Single(result, x => x.Title == "幂级数讲义");
        Assert.Contains("幂级数端点收敛", powerChunk.MatchedKnowledgePoints);
    }

    [Fact]
    public async Task RetrieveAsync_ShouldKeepImproperIntegralRetrievalWorking()
    {
        var db = TestDb.Create(nameof(RetrieveAsync_ShouldKeepImproperIntegralRetrievalWorking));
        SeedChunk(db,
            chunkId: 9006,
            materialId: 8006,
            chapterId: 307,
            title: "反常积分讲义",
            sectionTitle: "瑕点拆分与比较判别法",
            sectionPath: "反常积分/瑕点拆分",
            preview: "在瑕点处先拆分，再用比较判别法判断反常积分收敛性。",
            knowledgePointCodes: new[]
            {
                "ma.improper_integral.convergence_criteria",
                "ma.improper_integral.comparison_test",
                "ma.improper_integral.singularity_split"
            });

        var service = new KnowledgeRetrievalService(db);
        var result = await service.RetrieveAsync(new()
        {
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ProblemText = "反常积分的收敛性",
            TopK = 3
        });

        var chunk = Assert.Single(result, x => x.Title == "反常积分讲义");
        Assert.Contains("收敛与发散判定", chunk.MatchedKnowledgePoints);
    }

    private static void SeedChunk(
        ApplicationDbContext db,
        int chunkId,
        int materialId,
        int chapterId,
        string title,
        string sectionTitle,
        string sectionPath,
        string preview,
        IReadOnlyList<string> knowledgePointCodes)
    {
        var courseMaterial = new CourseMaterial
        {
            Id = materialId,
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            Title = title,
            MaterialKind = "pdf",
            Language = "zh-CN",
            Visibility = "course_internal",
            OriginalFileName = $"{title}.pdf",
            FileExtension = ".pdf",
            StoragePath = $"/materials/{materialId}.pdf",
            ParseStatus = "success",
            FileSizeBytes = 1024,
            UploadedAt = DateTime.UtcNow
        };

        var chunk = new MaterialChunk
        {
            Id = chunkId,
            CourseMaterialId = materialId,
            CourseId = PlatformSeedData.CourseMathAnalysisId,
            ChapterId = chapterId,
            ChunkIndex = 1,
            ChunkType = "definition",
            SemanticTitle = sectionTitle,
            SectionTitle = sectionTitle,
            SectionPath = sectionPath,
            Content = preview,
            ContentPreview = preview,
            TokenCountEstimate = 100,
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        db.CourseMaterials.Add(courseMaterial);
        db.MaterialChunks.Add(chunk);
        db.SaveChanges();

        foreach (var code in knowledgePointCodes)
        {
            var knowledgePointId = db.KnowledgePoints
                .Where(x => x.CourseId == PlatformSeedData.CourseMathAnalysisId && x.Code == code)
                .Select(x => x.Id)
                .First();

            db.MaterialChunkKnowledgePoints.Add(new MaterialChunkKnowledgePoint
            {
                MaterialChunkId = chunkId,
                KnowledgePointId = knowledgePointId,
                RelationType = "related",
                IsPrimary = true,
                Confidence = 0.95m,
                Source = "seed",
                CreatedAt = DateTime.UtcNow
            });
        }

        db.SaveChanges();
    }
}
