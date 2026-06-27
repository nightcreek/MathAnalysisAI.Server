using System.Text.RegularExpressions;
using MathAnalysisAI.Server.DTOs.Knowledge;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Knowledge
{
    public class KnowledgeRetrievalService : IKnowledgeRetrievalService
    {
        private static readonly string[] MathTerms =
        {
            "反常积分", "收敛", "发散", "比较判别法", "判别法", "p积分", "极限", "级数", "函数列", "一致收敛", "逐点收敛", "幂级数", "泰勒", "导数", "定积分", "不定积分", "重积分", "曲线积分", "曲面积分", "无穷"
        };

        private static readonly RetrievalHint[] RetrievalHints =
        {
            new("重积分", new[] { "重积分", "multiple integral" }),
            new("二重积分", new[] { "二重积分", "double integral" }),
            new("三重积分", new[] { "三重积分", "triple integral" }),
            new("积分次序", new[] { "积分次序", "order of integration" }),
            new("变量替换", new[] { "变量替换", "change of variables" }),
            new("极坐标", new[] { "极坐标", "polar coordinates" }),
            new("柱坐标", new[] { "柱坐标", "cylindrical coordinates" }),
            new("球坐标", new[] { "球坐标", "spherical coordinates" }),
            new("曲线积分", new[] { "曲线积分", "line integral" }),
            new("第一类曲线积分", new[] { "第一类曲线积分", "scalar line integral" }),
            new("第二类曲线积分", new[] { "第二类曲线积分", "vector line integral" }),
            new("路径无关性", new[] { "路径无关", "路径无关性", "path independent" }),
            new("保守场", new[] { "保守场", "conservative field" }),
            new("Green 公式", new[] { "Green 公式", "green formula" }),
            new("曲面积分", new[] { "曲面积分", "surface integral" }),
            new("第一类曲面积分", new[] { "第一类曲面积分", "scalar surface integral" }),
            new("第二类曲面积分", new[] { "第二类曲面积分", "flux integral" }),
            new("Gauss 公式", new[] { "Gauss 公式", "gauss formula" }),
            new("Stokes 公式", new[] { "Stokes 公式", "stokes formula" }),
            new("反常积分瑕点拆分", new[] { "反常积分瑕点拆分", "瑕点", "奇点", "improper integral singularity" }),
            new("幂级数端点", new[] { "幂级数端点", "幂级数端点收敛", "endpoint convergence" }),
            new("一致收敛", new[] { "一致收敛", "uniform convergence" }),
            new("逐点收敛", new[] { "逐点收敛", "pointwise convergence" }),
            new("函数项级数一致收敛", new[] { "函数项级数一致收敛", "function series uniform convergence" }),
            new("逐点收敛与一致收敛区分", new[] { "逐点收敛与一致收敛区分", "pointwise vs uniform convergence" }),
            new("泰勒公式余项", new[] { "泰勒公式余项", "泰勒余项", "remainder term" }),
            new("中值定理条件检查", new[] { "中值定理条件检查", "mean value theorem conditions" }),
            new("极限与积分交换条件", new[] { "极限与积分交换条件", "极限与积分交换", "interchange of limit and integral" }),
            new("反常积分", new[] { "反常积分", "improper integral" })
        };

        private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
        {
            "因为", "所以", "请", "请问", "判断", "这个", "那个", "进行", "并且", "然后", "我们", "你", "我", "他", "她", "它", "是否", "怎么", "如何", "求", "计算", "证明"
        };

        private readonly IPersistenceService _persistenceService;

        public KnowledgeRetrievalService(IPersistenceService persistenceService)
        {
            _persistenceService = persistenceService;
        }

        public async Task<IReadOnlyList<KnowledgeChunkContextDto>> RetrieveAsync(
            KnowledgeRetrievalRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || request.CourseId <= 0)
            {
                return Array.Empty<KnowledgeChunkContextDto>();
            }

            var topK = Math.Clamp(request.TopK <= 0 ? 3 : request.TopK, 1, 8);
            var keywords = BuildKeywords(request.ProblemText, request.StudentSolutionText);
            var requestedKnowledgePointCodes = await ResolveRequestedKnowledgePointCodesAsync(
                request.CourseId,
                request.NormalizedKnowledgePointCodes,
                request.ProblemText,
                request.StudentSolutionText,
                request.ChapterId,
                cancellationToken);

            var materialChunks = await _persistenceService.GetKnowledgeRetrievalMaterialChunksAsync(
                new MaterialChunkRetrievalQuery(request.CourseId, request.ChapterId, 600),
                cancellationToken);
            var candidates = materialChunks
                .Where(c => c.CourseMaterial != null)
                .Select(c => new ChunkCandidate
                {
                    ChunkId = c.Id,
                    MaterialId = c.CourseMaterialId,
                    CourseId = c.CourseId,
                    ChapterId = c.ChapterId,
                    Title = c.CourseMaterial!.Title,
                    MaterialKind = c.CourseMaterial.MaterialKind,
                    UploadedAt = c.CourseMaterial.UploadedAt,
                    SectionTitle = c.SectionTitle,
                    SectionPath = c.SectionPath,
                    PageStart = c.PageStart,
                    PageEnd = c.PageEnd,
                    ChunkType = c.ChunkType,
                    ContentPreview = c.ContentPreview,
                    IsVerified = c.IsVerified
                })
                .ToList();

            if (candidates.Count == 0)
            {
                return Array.Empty<KnowledgeChunkContextDto>();
            }

            var candidateChunkIds = candidates.Select(c => c.ChunkId).ToList();
            var mappedKnowledgePoints = await ResolveRequestedKnowledgePointsAsync(
                request.CourseId,
                requestedKnowledgePointCodes,
                cancellationToken);

            var candidateLinks = (await _persistenceService.GetMaterialChunkKnowledgePointLinksAsync(
                    new MaterialChunkKnowledgePointLinksQuery(candidateChunkIds),
                    cancellationToken))
                .Select(x => new
                {
                    x.MaterialChunkId,
                    x.KnowledgePointId,
                    x.IsPrimary,
                    x.Confidence,
                    KnowledgePointCode = x.KnowledgePoint?.Code,
                    KnowledgePointName = x.KnowledgePoint?.Name
                })
                .ToList();

            var linksByChunk = candidateLinks
                .GroupBy(x => x.MaterialChunkId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var results = new List<ScoredChunk>(candidates.Count);
            foreach (var candidate in candidates)
            {
                var score = 0m;

                if (request.ChapterId.HasValue && candidate.ChapterId == request.ChapterId.Value)
                {
                    score += 2m;
                }

                if (keywords.Count > 0)
                {
                    score += ScoreTitleSectionAndPath(candidate, keywords);
                    score += ScoreContentPreview(candidate.ContentPreview, keywords);
                }

                score += ScoreChunkType(candidate.ChunkType);

                if (candidate.IsVerified)
                {
                    score += 1m;
                }

                var matchedKnowledgePointTexts = new List<string>();
                if (linksByChunk.TryGetValue(candidate.ChunkId, out var links))
                {
                    foreach (var link in links)
                    {
                        if (!mappedKnowledgePoints.ContainsKey(link.KnowledgePointId))
                        {
                            continue;
                        }

                        score += 2m;
                        if (link.IsPrimary)
                        {
                            score += 1m;
                        }

                        var confidenceBonus = Math.Min(link.Confidence, 1m) * 0.5m;
                        score += confidenceBonus;

                        var shortInfo = !string.IsNullOrWhiteSpace(link.KnowledgePointName)
                            ? link.KnowledgePointName!
                            : (!string.IsNullOrWhiteSpace(link.KnowledgePointCode) ? link.KnowledgePointCode! : string.Empty);

                        if (!string.IsNullOrWhiteSpace(shortInfo)
                            && !matchedKnowledgePointTexts.Any(x => string.Equals(x, shortInfo, StringComparison.OrdinalIgnoreCase)))
                        {
                            matchedKnowledgePointTexts.Add(shortInfo);
                        }
                    }
                }

                if (score <= 0m)
                {
                    continue;
                }

                results.Add(new ScoredChunk
                {
                    Candidate = candidate,
                    Score = Math.Round(score, 3),
                    MatchedKnowledgePoints = matchedKnowledgePointTexts
                });
            }

            var ordered = results
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Candidate.IsVerified)
                .ThenByDescending(x => x.Candidate.UploadedAt)
                .Take(topK)
                .Select(x => new KnowledgeChunkContextDto
                {
                    ChunkId = x.Candidate.ChunkId,
                    MaterialId = x.Candidate.MaterialId,
                    Title = x.Candidate.Title,
                    MaterialKind = x.Candidate.MaterialKind,
                    SectionTitle = x.Candidate.SectionTitle,
                    SectionPath = x.Candidate.SectionPath,
                    PageStart = x.Candidate.PageStart,
                    PageEnd = x.Candidate.PageEnd,
                    ChunkType = x.Candidate.ChunkType,
                    ContentPreview = Truncate(x.Candidate.ContentPreview, 500),
                    MatchedKnowledgePoints = x.MatchedKnowledgePoints,
                    Score = x.Score,
                    SourceLabel = "sql_keyword"
                })
                .ToList();

            return ordered;
        }

        private async Task<IReadOnlyList<string>> ResolveRequestedKnowledgePointCodesAsync(
            int courseId,
            IReadOnlyList<string>? normalizedKnowledgePointCodes,
            string problemText,
            string? studentSolutionText,
            int? chapterId,
            CancellationToken cancellationToken)
        {
            if (normalizedKnowledgePointCodes != null && normalizedKnowledgePointCodes.Count > 0)
            {
                return normalizedKnowledgePointCodes
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }

            var labels = InferKnowledgePointLabels(problemText, studentSolutionText);
            if (labels.Count == 0)
            {
                return Array.Empty<string>();
            }

            return await _persistenceService.NormalizeKnowledgePointsAsync(
                new NormalizeKnowledgePointsQuery(
                    labels,
                    courseId,
                    chapterId,
                    problemText,
                    studentSolutionText),
                cancellationToken);
        }

        private async Task<Dictionary<int, string>> ResolveRequestedKnowledgePointsAsync(
            int courseId,
            IReadOnlyList<string>? normalizedKnowledgePointCodes,
            CancellationToken cancellationToken)
        {
            if (normalizedKnowledgePointCodes == null || normalizedKnowledgePointCodes.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var codes = normalizedKnowledgePointCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (codes.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var points = await _persistenceService.GetKnowledgePointsByCodesAsync(
                new KnowledgePointsByCodesQuery(courseId, codes),
                cancellationToken);

            return points
                .Where(x => !string.IsNullOrWhiteSpace(x.Code))
                .ToDictionary(x => x.Id, x => x.Code!);
        }

        private static List<string> InferKnowledgePointLabels(string? problemText, string? studentSolutionText)
        {
            var combined = $"{problemText} {studentSolutionText}";
            if (string.IsNullOrWhiteSpace(combined))
            {
                return new List<string>();
            }

            var normalized = combined
                .Replace("（", "(")
                .Replace("）", ")")
                .Replace(" ", string.Empty)
                .ToLowerInvariant();

            var labels = new List<string>();
            foreach (var hint in RetrievalHints)
            {
                if (hint.Phrases.Any(phrase => ContainsNormalized(normalized, phrase)))
                {
                    labels.Add(hint.Label);
                }
            }

            return labels
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static decimal ScoreTitleSectionAndPath(ChunkCandidate candidate, IReadOnlyList<string> keywords)
        {
            decimal score = 0m;
            foreach (var keyword in keywords)
            {
                if (Contains(candidate.Title, keyword)) score += 1m;
                if (Contains(candidate.SectionTitle, keyword)) score += 1m;
                if (Contains(candidate.SectionPath, keyword)) score += 1m;
            }
            return score;
        }

        private static decimal ScoreContentPreview(string? preview, IReadOnlyList<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(preview) || keywords.Count == 0)
            {
                return 0m;
            }

            decimal score = 0m;
            foreach (var keyword in keywords)
            {
                if (Contains(preview, keyword))
                {
                    score += 1.5m;
                }
            }
            return score;
        }

        private static decimal ScoreChunkType(string? chunkType)
        {
            var normalized = (chunkType ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                "definition" => 1.5m,
                "theorem" => 1.5m,
                "method" => 1.5m,
                "example" => 1m,
                _ => 0m
            };
        }

        private static List<string> BuildKeywords(string? problemText, string? studentSolutionText)
        {
            var combined = (problemText ?? string.Empty) + " " + (studentSolutionText ?? string.Empty);
            if (string.IsNullOrWhiteSpace(combined))
            {
                return new List<string>();
            }

            var expanded = combined
                .Replace("\\int", " 积分 ", StringComparison.OrdinalIgnoreCase)
                .Replace("∫", " 积分 ", StringComparison.OrdinalIgnoreCase)
                .Replace("\\infty", " 无穷 ", StringComparison.OrdinalIgnoreCase)
                .Replace("∞", " 无穷 ", StringComparison.OrdinalIgnoreCase)
                .Replace("\\sum", " 级数 ", StringComparison.OrdinalIgnoreCase)
                .Replace("\\lim", " 极限 ", StringComparison.OrdinalIgnoreCase);

            var words = new List<string>();
            foreach (var term in MathTerms)
            {
                if (expanded.Contains(term, StringComparison.OrdinalIgnoreCase))
                {
                    words.Add(term);
                }
            }

            foreach (var hint in RetrievalHints)
            {
                if (hint.Phrases.Any(phrase => expanded.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
                {
                    words.Add(hint.Label);
                }
            }

            var tokens = Regex.Split(expanded, "[^\\p{L}\\p{Nd}_]+")
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Where(x => x.Length >= 2)
                .Where(x => !Stopwords.Contains(x))
                .Where(x => !string.Equals(x, "积分", StringComparison.OrdinalIgnoreCase));

            words.AddRange(tokens);

            var result = words
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .ToList();

            return result;
        }

        private static bool Contains(string? text, string keyword)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && !string.IsNullOrWhiteSpace(keyword)
                   && text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsNormalized(string text, string phrase)
        {
            return !string.IsNullOrWhiteSpace(text)
                   && !string.IsNullOrWhiteSpace(phrase)
                   && text.Contains(phrase.Replace(" ", string.Empty).ToLowerInvariant(), StringComparison.OrdinalIgnoreCase);
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value[..maxLength];
        }

        private sealed class ChunkCandidate
        {
            public int ChunkId { get; set; }
            public int MaterialId { get; set; }
            public int CourseId { get; set; }
            public int? ChapterId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string MaterialKind { get; set; } = string.Empty;
            public DateTime UploadedAt { get; set; }
            public string? SectionTitle { get; set; }
            public string? SectionPath { get; set; }
            public int? PageStart { get; set; }
            public int? PageEnd { get; set; }
            public string ChunkType { get; set; } = "unknown";
            public string ContentPreview { get; set; } = string.Empty;
            public bool IsVerified { get; set; }
        }

        private sealed class ScoredChunk
        {
            public ChunkCandidate Candidate { get; set; } = new();
            public decimal Score { get; set; }
            public List<string> MatchedKnowledgePoints { get; set; } = new();
        }

        private sealed record RetrievalHint(string Label, string[] Phrases);
    }
}
