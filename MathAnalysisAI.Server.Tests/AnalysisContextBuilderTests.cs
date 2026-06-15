using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Analysis.Context;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace MathAnalysisAI.Server.Tests;

public class AnalysisContextBuilderTests
{
    [Fact]
    public async Task BuildAsync_ShouldFormatMultipleIntegralContextClearly()
    {
        var builder = CreateBuilder(new[]
        {
            CreateChunk("重积分讲义", new[] { "重积分", "积分区域与积分次序", "二重积分" }, "二重积分改变积分次序")
        });

        var context = await builder.BuildAsync(CreateRequest("二重积分改变积分次序"));

        Assert.True(context.HasAnyContext);
        Assert.Contains("本题可能涉及以下数学分析知识点：", context.PromptContextBlock);
        Assert.Contains("相关课程材料摘要：", context.PromptContextBlock);
        Assert.Contains("二重积分", context.PromptContextBlock);
        Assert.Equal("二重积分", context.KnowledgeChunks[0].MatchedKnowledgePoints[0]);
    }

    [Fact]
    public async Task BuildAsync_ShouldFormatLineIntegralContextClearly()
    {
        var builder = CreateBuilder(new[]
        {
            CreateChunk("曲线积分讲义", new[] { "路径无关性 / 保守场", "第二类曲线积分", "曲线积分" }, "第二类曲线积分与路径无关")
        });

        var context = await builder.BuildAsync(CreateRequest("第二类曲线积分与路径无关"));

        Assert.Contains("优先复核这些定理条件：", context.PromptContextBlock);
        Assert.Contains("路径无关性 / 保守场", context.PromptContextBlock);
        Assert.Contains("第二类曲线积分", context.PromptContextBlock);
    }

    [Fact]
    public async Task BuildAsync_ShouldFormatConvergenceContextClearly()
    {
        var builder = CreateBuilder(new[]
        {
            CreateChunk("函数项级数讲义", new[] { "函数项级数一致收敛", "逐点收敛与一致收敛区分", "极限与积分交换条件" }, "一致收敛与逐点收敛")
        });

        var context = await builder.BuildAsync(CreateRequest("一致收敛与逐点收敛"));

        Assert.Contains("注意区分以下易错概念：", context.PromptContextBlock);
        Assert.Contains("函数项级数一致收敛", context.PromptContextBlock);
        Assert.Contains("逐点收敛与一致收敛区分", context.PromptContextBlock);
    }

    [Fact]
    public async Task BuildAsync_ShouldKeepImproperIntegralContextWorking()
    {
        var builder = CreateBuilder(new[]
        {
            CreateChunk("反常积分讲义", new[] { "反常积分瑕点拆分", "反常积分收敛与发散判定" }, "瑕点拆分")
        });

        var context = await builder.BuildAsync(CreateRequest("反常积分瑕点拆分"));

        Assert.Contains("反常积分瑕点拆分", context.PromptContextBlock);
        Assert.Contains("反常积分收敛与发散判定", context.PromptContextBlock);
    }

    private static AnalysisContextBuilder CreateBuilder(IReadOnlyList<KnowledgeChunkContextDto> chunks)
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AnalysisContextOptions
        {
            EnableKnowledgeRetrieval = true,
            KnowledgeTopK = 3,
            MaxKnowledgeContextChars = 1200,
            MaxChunkPreviewChars = 400
        });
        return new AnalysisContextBuilder(
            options,
            new FakeKnowledgeRetrievalService(chunks),
            NullLogger<AnalysisContextBuilder>.Instance);
    }

    private static AnalysisContextBuildRequest CreateRequest(string problemText)
    {
        return new AnalysisContextBuildRequest
        {
            Request = new AnalysisRequestDto
            {
                CourseId = 200,
                ProblemText = problemText,
                AnalysisMode = "review_solution"
            },
            Course = new MathAnalysisAI.Server.Models.Course
            {
                Id = 200,
                Name = "数学分析"
            },
            Problem = new MathAnalysisAI.Server.Models.Problem
            {
                Id = 1,
                CourseId = 200,
                ContentMarkdown = problemText
            }
        };
    }

    private static KnowledgeChunkContextDto CreateChunk(string title, IEnumerable<string> knowledgePoints, string preview)
    {
        return new KnowledgeChunkContextDto
        {
            ChunkId = 1,
            MaterialId = 1,
            Title = title,
            MaterialKind = "pdf",
            SectionTitle = title,
            SectionPath = title,
            PageStart = 1,
            PageEnd = 2,
            ChunkType = "definition",
            ContentPreview = preview,
            MatchedKnowledgePoints = knowledgePoints.ToList(),
            Score = 1m,
            SourceLabel = "sql_keyword"
        };
    }

    private sealed class FakeKnowledgeRetrievalService : IKnowledgeRetrievalService
    {
        private readonly IReadOnlyList<KnowledgeChunkContextDto> _chunks;

        public FakeKnowledgeRetrievalService(IReadOnlyList<KnowledgeChunkContextDto> chunks)
        {
            _chunks = chunks;
        }

        public Task<IReadOnlyList<KnowledgeChunkContextDto>> RetrieveAsync(KnowledgeRetrievalRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_chunks);
        }
    }
}
