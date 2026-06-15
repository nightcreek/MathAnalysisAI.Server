using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.Extensions.Options;
using System.Text;

namespace MathAnalysisAI.Server.Services.Analysis.Context;

public sealed class AnalysisContextBuilder : IAnalysisContextBuilder
{
    private readonly AnalysisContextOptions _options;
    private readonly IKnowledgeRetrievalService _knowledgeRetrievalService;
    private readonly ILogger<AnalysisContextBuilder> _logger;

    public AnalysisContextBuilder(
        IOptions<AnalysisContextOptions> options,
        IKnowledgeRetrievalService knowledgeRetrievalService,
        ILogger<AnalysisContextBuilder> logger)
    {
        _options = options.Value;
        _knowledgeRetrievalService = knowledgeRetrievalService;
        _logger = logger;
    }

    public Task<AnalysisContextDto> BuildAsync(
        AnalysisContextBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableKnowledgeRetrieval)
        {
            return Task.FromResult(AnalysisContextDto.Empty);
        }

        return BuildWithRetrievalAsync(request, cancellationToken);
    }

    private async Task<AnalysisContextDto> BuildWithRetrievalAsync(
        AnalysisContextBuildRequest request,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        try
        {
            var topK = Math.Clamp(_options.KnowledgeTopK, 1, 8);
            var maxKnowledgeChars = Math.Max(200, _options.MaxKnowledgeContextChars);
            var maxChunkPreviewChars = Math.Max(100, _options.MaxChunkPreviewChars);

            var retrievalRequest = new KnowledgeRetrievalRequest
            {
                CourseId = request.Request.CourseId,
                ChapterId = request.Request.ChapterId,
                ProblemText = request.Request.ProblemText,
                StudentSolutionText = request.Request.StudentSolutionText,
                TopK = topK,
                NormalizedKnowledgePointCodes = null
            };

            var chunks = await _knowledgeRetrievalService.RetrieveAsync(retrievalRequest, cancellationToken);
            if (chunks.Count == 0)
            {
                return AnalysisContextDto.Empty;
            }

            var trimmedChunks = chunks
                .Select(x => TrimChunk(x, maxChunkPreviewChars))
                .ToList();

            var promptContextBlock = BuildPromptContextBlock(trimmedChunks, maxKnowledgeChars);
            if (string.IsNullOrWhiteSpace(promptContextBlock))
            {
                return new AnalysisContextDto
                {
                    KnowledgeChunks = trimmedChunks,
                    SymbolicEvidences = Array.Empty<SymbolicEvidenceDto>(),
                    PromptContextBlock = string.Empty,
                    Warnings = warnings,
                    HasAnyContext = false
                };
            }

            return new AnalysisContextDto
            {
                KnowledgeChunks = trimmedChunks,
                SymbolicEvidences = Array.Empty<SymbolicEvidenceDto>(),
                PromptContextBlock = promptContextBlock,
                Warnings = warnings,
                HasAnyContext = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to build knowledge retrieval analysis context for CourseId={CourseId}, ChapterId={ChapterId}", request.Request.CourseId, request.Request.ChapterId);
            warnings.Add("knowledge_retrieval_failed");
            return new AnalysisContextDto
            {
                KnowledgeChunks = Array.Empty<KnowledgeChunkContextDto>(),
                SymbolicEvidences = Array.Empty<SymbolicEvidenceDto>(),
                PromptContextBlock = string.Empty,
                Warnings = warnings,
                HasAnyContext = false
            };
        }
    }

    private static KnowledgeChunkContextDto TrimChunk(KnowledgeChunkContextDto source, int maxChunkPreviewChars)
    {
        return new KnowledgeChunkContextDto
        {
            ChunkId = source.ChunkId,
            MaterialId = source.MaterialId,
            Title = source.Title,
            MaterialKind = source.MaterialKind,
            SectionTitle = source.SectionTitle,
            SectionPath = source.SectionPath,
            PageStart = source.PageStart,
            PageEnd = source.PageEnd,
            ChunkType = source.ChunkType,
            ContentPreview = TrimText(source.ContentPreview, maxChunkPreviewChars),
            MatchedKnowledgePoints = KnowledgePointLabelHelper.NormalizeAndSort(source.MatchedKnowledgePoints),
            Score = source.Score,
            SourceLabel = source.SourceLabel
        };
    }

    private static string BuildPromptContextBlock(IReadOnlyList<KnowledgeChunkContextDto> chunks, int maxKnowledgeChars)
    {
        if (chunks.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        sb.AppendLine("[课程资料参考片段]");

        var allKnowledgePoints = CollectAllKnowledgePoints(chunks);
        AppendKnowledgeHeaders(sb, allKnowledgePoints);
        AppendChunkAbstracts(sb, chunks, maxKnowledgeChars);

        sb.AppendLine("说明：以上资料片段仅供参考，请结合题意进行分析。");

        var text = sb.ToString().Trim();
        return text.Length > maxKnowledgeChars ? text[..maxKnowledgeChars] : text;
    }

    private static List<string> CollectAllKnowledgePoints(IReadOnlyList<KnowledgeChunkContextDto> chunks)
    {
        return chunks
            .SelectMany(x => x.MatchedKnowledgePoints ?? new List<string>())
            .Select(KnowledgePointLabelHelper.NormalizeLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(KnowledgePointLabelHelper.GetPriority)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendKnowledgeHeaders(StringBuilder sb, List<string> allKnowledgePoints)
    {
        if (allKnowledgePoints.Count > 0)
        {
            AppendSection(sb, "本题可能涉及以下数学分析知识点：", allKnowledgePoints.Take(8));
        }

        var theoremPoints = allKnowledgePoints
            .Where(KnowledgePointLabelHelper.IsTheoremCondition)
            .Take(6)
            .ToList();
        if (theoremPoints.Count > 0)
        {
            AppendSection(sb, "优先复核这些定理条件：", theoremPoints);
        }

        var confusingPoints = allKnowledgePoints
            .Where(KnowledgePointLabelHelper.IsEasyConfusion)
            .Take(6)
            .ToList();
        if (confusingPoints.Count > 0)
        {
            AppendSection(sb, "注意区分以下易错概念：", confusingPoints);
        }
    }

    private static void AppendChunkAbstracts(StringBuilder sb, IReadOnlyList<KnowledgeChunkContextDto> chunks, int maxKnowledgeChars)
    {
        sb.AppendLine("相关课程材料摘要：");

        var index = 1;
        foreach (var chunk in chunks)
        {
            var section = !string.IsNullOrWhiteSpace(chunk.SectionTitle)
                ? chunk.SectionTitle
                : (string.IsNullOrWhiteSpace(chunk.SectionPath) ? "未标注" : chunk.SectionPath);
            var page = BuildPageLabel(chunk.PageStart, chunk.PageEnd);
            var matched = (chunk.MatchedKnowledgePoints != null && chunk.MatchedKnowledgePoints.Count > 0)
                ? string.Join(", ", chunk.MatchedKnowledgePoints)
                : "无";

            sb.AppendLine($"{index}) 来源：{chunk.Title} | 类型：{chunk.MaterialKind} | 章节：{section} | 页码：{page}");
            sb.AppendLine($"   摘要：{chunk.ContentPreview}");
            sb.AppendLine($"   关联知识点：{matched}");
            index++;

            if (sb.Length >= maxKnowledgeChars)
            {
                break;
            }
        }
    }

    private static string BuildPageLabel(int? pageStart, int? pageEnd)
    {
        if (pageStart.HasValue && pageEnd.HasValue)
        {
            return pageStart.Value == pageEnd.Value
                ? pageStart.Value.ToString()
                : $"{pageStart.Value}-{pageEnd.Value}";
        }

        if (pageStart.HasValue)
        {
            return pageStart.Value.ToString();
        }

        if (pageEnd.HasValue)
        {
            return pageEnd.Value.ToString();
        }

        return "未标注";
    }

    private static string TrimText(string? input, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var text = input.Trim();
        return text.Length <= maxChars ? text : text[..maxChars];
    }

    private static void AppendSection(StringBuilder sb, string title, IEnumerable<string> items)
    {
        sb.AppendLine(title);
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            sb.AppendLine($"- {item}");
        }
    }
}
