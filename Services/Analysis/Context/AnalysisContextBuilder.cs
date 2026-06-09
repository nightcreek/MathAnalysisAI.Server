using MathAnalysisAI.Server.DTOs.AnalysisContext;
using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Services.Knowledge;
using System.Text;

namespace MathAnalysisAI.Server.Services.Analysis.Context;

public sealed class AnalysisContextBuilder : IAnalysisContextBuilder
{
    private readonly IConfiguration _configuration;
    private readonly IKnowledgeRetrievalService _knowledgeRetrievalService;
    private readonly ILogger<AnalysisContextBuilder> _logger;

    public AnalysisContextBuilder(
        IConfiguration configuration,
        IKnowledgeRetrievalService knowledgeRetrievalService,
        ILogger<AnalysisContextBuilder> logger)
    {
        _configuration = configuration;
        _knowledgeRetrievalService = knowledgeRetrievalService;
        _logger = logger;
    }

    public Task<AnalysisContextDto> BuildAsync(
        AnalysisContextBuildRequest request,
        CancellationToken cancellationToken = default)
    {
        var retrievalEnabled = _configuration.GetValue("AnalysisContext:EnableKnowledgeRetrieval", false);
        if (!retrievalEnabled)
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
            var topK = Math.Clamp(_configuration.GetValue("AnalysisContext:KnowledgeTopK", 3), 1, 8);
            var maxKnowledgeChars = Math.Max(200, _configuration.GetValue("AnalysisContext:MaxKnowledgeContextChars", 1200));
            var maxChunkPreviewChars = Math.Max(100, _configuration.GetValue("AnalysisContext:MaxChunkPreviewChars", 400));

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
            MatchedKnowledgePoints = NormalizeMatchedKnowledgePoints(source.MatchedKnowledgePoints),
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

        var allKnowledgePoints = chunks
            .SelectMany(x => x.MatchedKnowledgePoints ?? new List<string>())
            .Select(NormalizeKnowledgePointLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetKnowledgePointPriority)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (allKnowledgePoints.Count > 0)
        {
            AppendSection(sb, "本题可能涉及以下数学分析知识点：", allKnowledgePoints.Take(8));
        }

        var theoremPoints = allKnowledgePoints
            .Where(IsTheoremConditionLabel)
            .Take(6)
            .ToList();
        if (theoremPoints.Count > 0)
        {
            AppendSection(sb, "优先复核这些定理条件：", theoremPoints);
        }

        var confusingPoints = allKnowledgePoints
            .Where(IsEasyConfusionLabel)
            .Take(6)
            .ToList();
        if (confusingPoints.Count > 0)
        {
            AppendSection(sb, "注意区分以下易错概念：", confusingPoints);
        }

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

        sb.AppendLine("说明：以上资料片段仅供参考，请结合题意进行分析。");

        var text = sb.ToString().Trim();
        return text.Length > maxKnowledgeChars
            ? text[..maxKnowledgeChars]
            : text;
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
        return text.Length <= maxChars
            ? text
            : text[..maxChars];
    }

    private static List<string> NormalizeMatchedKnowledgePoints(IEnumerable<string>? points)
    {
        return (points ?? Enumerable.Empty<string>())
            .Select(NormalizeKnowledgePointLabel)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetKnowledgePointPriority)
            .ThenBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void AppendSection(StringBuilder sb, string title, IEnumerable<string> items)
    {
        sb.AppendLine(title);
        foreach (var item in items.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            sb.AppendLine($"- {item}");
        }
    }

    private static string NormalizeKnowledgePointLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var value = raw.Trim();
        if (!value.StartsWith("ma.", StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        return value switch
        {
            "ma.multiple_integral.concept" => "重积分",
            "ma.multiple_integral.double_integral" => "二重积分",
            "ma.multiple_integral.triple_integral" => "三重积分",
            "ma.multiple_integral.region_order" => "积分区域与积分次序",
            "ma.multiple_integral.change_of_variables" => "重积分变量替换",
            "ma.multiple_integral.coordinate_systems" => "极坐标 / 柱坐标 / 球坐标",
            "重积分概念" => "重积分",
            "二重积分定义与计算" => "二重积分",
            "三重积分定义与计算" => "三重积分",
            "积分区域与积分次序" => "积分区域与积分次序",
            "重积分变量替换" => "重积分变量替换",
            "坐标系下的重积分" => "极坐标 / 柱坐标 / 球坐标",
            "ma.line_integral.concept" => "曲线积分",
            "ma.line_integral.scalar" => "第一类曲线积分",
            "ma.line_integral.vector" => "第二类曲线积分",
            "ma.line_integral.path_independence" => "路径无关性 / 保守场",
            "ma.line_integral.green_formula" => "Green 公式",
            "曲线积分概念" => "曲线积分",
            "第一类曲线积分" => "第一类曲线积分",
            "第二类曲线积分" => "第二类曲线积分",
            "路径无关性与保守场" => "路径无关性 / 保守场",
            "Green 公式" => "Green 公式",
            "ma.surface_integral.concept" => "曲面积分",
            "ma.surface_integral.scalar" => "第一类曲面积分",
            "ma.surface_integral.flux" => "第二类曲面积分",
            "ma.surface_integral.gauss_formula" => "Gauss 公式",
            "ma.surface_integral.stokes_formula" => "Stokes 公式",
            "曲面积分概念" => "曲面积分",
            "第一类曲面积分" => "第一类曲面积分",
            "第二类曲面积分" => "第二类曲面积分",
            "Gauss 公式" => "Gauss 公式",
            "Stokes 公式" => "Stokes 公式",
            "ma.improper_integral.singularity_split" => "反常积分瑕点拆分",
            "ma.power_series.endpoint_convergence" => "幂级数端点收敛",
            "ma.function_series.uniform_convergence.criteria" => "函数项级数一致收敛",
            "ma.function_series.pointwise_convergence" => "逐点收敛",
            "ma.function_series.pointwise_vs_uniform" => "逐点收敛与一致收敛区分",
            "ma.power_series.taylor_remainder" => "泰勒公式余项",
            "ma.mean_value_theorem.conditions_check" => "中值定理条件检查",
            "ma.function_series.limit_exchange_conditions" => "极限与积分交换条件",
            "ma.improper_integral.convergence_criteria" => "反常积分收敛与发散判定",
            "ma.improper_integral.comparison_test" => "反常积分比较判别法",
            "反常积分瑕点拆分" => "反常积分瑕点拆分",
            "幂级数端点收敛" => "幂级数端点收敛",
            "一致收敛定义" => "函数项级数一致收敛",
            "函数列逐点收敛" => "逐点收敛",
            "逐点收敛与一致收敛的区别" => "逐点收敛与一致收敛区分",
            "泰勒公式余项" => "泰勒公式余项",
            "使用中值定理前的条件检查" => "中值定理条件检查",
            "极限与积分交换的条件" => "极限与积分交换条件",
            "收敛与发散判定" => "反常积分收敛与发散判定",
            "比较判别法" => "反常积分比较判别法",
            _ => value
        };
    }

    private static int GetKnowledgePointPriority(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return 99;
        }

        var specificTerms = new[]
        {
            "二重积分", "三重积分", "第一类曲线积分", "第二类曲线积分", "第一类曲面积分", "第二类曲面积分",
            "路径无关性", "保守场", "Gauss 公式", "Stokes 公式", "幂级数端点收敛", "泰勒公式余项",
            "反常积分瑕点拆分", "中值定理条件检查", "极限与积分交换条件"
        };

        if (specificTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 0;
        }

        var conditionTerms = new[] { "条件", "定理", "公式", "交换", "收敛" };
        if (conditionTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 1;
        }

        var confusionTerms = new[] { "逐点收敛", "一致收敛", "端点", "瑕点", "余项" };
        if (confusionTerms.Any(term => label.Contains(term, StringComparison.OrdinalIgnoreCase)))
        {
            return 2;
        }

        return 3;
    }

    private static bool IsTheoremConditionLabel(string label)
    {
        return label.Contains("条件", StringComparison.OrdinalIgnoreCase)
            || label.Contains("定理", StringComparison.OrdinalIgnoreCase)
            || label.Contains("公式", StringComparison.OrdinalIgnoreCase)
            || label.Contains("交换", StringComparison.OrdinalIgnoreCase)
            || label.Contains("路径无关", StringComparison.OrdinalIgnoreCase)
            || label.Contains("保守场", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsEasyConfusionLabel(string label)
    {
        return label.Contains("一致收敛", StringComparison.OrdinalIgnoreCase)
            || label.Contains("逐点收敛", StringComparison.OrdinalIgnoreCase)
            || label.Contains("端点", StringComparison.OrdinalIgnoreCase)
            || label.Contains("瑕点", StringComparison.OrdinalIgnoreCase)
            || label.Contains("余项", StringComparison.OrdinalIgnoreCase);
    }
}
