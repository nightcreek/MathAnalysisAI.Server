using System.Text.Json;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Domain;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.SharedKernel.Analysis;

namespace MathAnalysisAI.Server.Services.Analysis.Verification
{
    public sealed class AnalysisVerificationService : IAnalysisVerificationService
    {
        private readonly IPersistenceService _persistenceService;

        public AnalysisVerificationService(IPersistenceService persistenceService)
        {
            _persistenceService = persistenceService;
        }

        public async Task<AnalysisVerificationResult> VerifyAsync(
            StructuredProblem? structuredProblem,
            PhotoSolutionOcrRecord? ocrRecord,
            AnalysisResult analysisResult,
            AnalysisResultModel? parsed,
            string? fallbackProblemText = null,
            string? fallbackStudentSolutionText = null,
            CancellationToken cancellationToken = default)
        {
            structuredProblem ??= await LoadStructuredProblemAsync(analysisResult.StructuredProblemId, cancellationToken);
            ocrRecord ??= await LoadOcrRecordAsync(structuredProblem?.PhotoSolutionOcrRecordId ?? analysisResult.PhotoSolutionOcrRecordId, cancellationToken);
            var problem = await LoadProblemAsync(analysisResult.ProblemId, cancellationToken);

            var result = new VerificationAccumulator();
            var problemText = NormalizeText(structuredProblem?.NormalizedProblemText ?? fallbackProblemText ?? problem?.ContentMarkdown);
            var studentText = NormalizeText(structuredProblem?.StudentSolutionText ?? fallbackStudentSolutionText ?? analysisResult.StudentSolution?.SolutionText);
            var standardSolutionText = NormalizeText(GetStandardSolutionText(parsed, analysisResult));
            var rawResponseText = NormalizeText(analysisResult.RawResponseJson);

            if (structuredProblem?.Status == StructuredProblemStatus.NeedsReview)
            {
                result.AddReason("structured_problem_needs_review", "结构化题目状态为 NeedsReview。", AnswerReliability.NeedsReview);
            }

            if (string.IsNullOrWhiteSpace(problemText) || RemoveWhitespace(problemText).Length < 8)
            {
                result.AddReason("problem_text_too_short", "结构化题干为空或过短。", AnswerReliability.Uncertain);
            }

            if (ContainsUnclear(problemText) || ContainsUnclear(studentText))
            {
                result.AddReason("contains_unclear", "题目或学生解答中包含 [unclear]。", AnswerReliability.UnsafeToUse);
            }

            if (ocrRecord != null && ParseWarnings(ocrRecord.WarningsJson).Count > 0)
            {
                result.AddReason("ocr_warnings_present", "OCR 结果存在 warnings。", AnswerReliability.NeedsReview);
            }

            if (IsFallbackOrParseFailed(rawResponseText))
            {
                result.AddReason("analysis_fallback_or_parse_failed", "分析结果来自 fallback / parse failed / schema invalid。", AnswerReliability.Uncertain);
            }

            if (!IsFallbackOrParseFailed(rawResponseText) && string.IsNullOrWhiteSpace(standardSolutionText))
            {
                result.AddReason("standard_solution_empty", "标准解答为空。", AnswerReliability.UnsafeToUse);
            }

            ApplyTopicChecks(problemText, standardSolutionText, result);

            var reliabilityReasonsJson = JsonSerializer.Serialize(result.ReliabilityReasons);
            var verifierWarningsJson = JsonSerializer.Serialize(result.VerifierWarnings);
            var verifiedAt = DateTime.UtcNow;

            analysisResult = await _persistenceService.SaveVerificationAsync(
                analysisResult,
                structuredProblem,
                ocrRecord,
                result.AnswerReliability,
                result.AnswerReliability != AnswerReliability.Reliable,
                reliabilityReasonsJson,
                verifierWarningsJson,
                verifiedAt,
                cancellationToken);

            return new AnalysisVerificationResult
            {
                AnswerReliability = analysisResult.AnswerReliability,
                NeedsReview = analysisResult.NeedsReview,
                ReliabilityReasons = ParseStrings(analysisResult.ReliabilityReasonsJson),
                VerifierWarnings = ParseStrings(analysisResult.VerifierWarningsJson),
                VerifiedAt = analysisResult.VerifiedAt ?? DateTime.UtcNow
            };
        }

        private static void ApplyTopicChecks(string problemText, string standardSolutionText, VerificationAccumulator result)
        {
            var text = problemText.ToLowerInvariant();
            var answer = standardSolutionText.ToLowerInvariant();

            if (text.Contains("反常积分") || text.Contains("瑕积分"))
            {
                if (!ContainsAny(answer, "瑕点", "无穷", "无穷区间"))
                {
                    result.AddReason("improper_integral_missing_endpoint_or_singularity_check", "反常积分题未见瑕点/无穷区间检查。", AnswerReliability.NeedsReview);
                }
            }

            if (text.Contains("函数项级数") || (text.Contains("级数") && !text.Contains("幂级数") && !text.Contains("泰勒")))
            {
                if (!ContainsAny(answer, "逐点", "一致收敛", "一致"))
                {
                    result.AddReason("series_missing_pointwise_uniform_check", "函数项级数题未区分逐点收敛与一致收敛。", AnswerReliability.NeedsReview);
                }
            }

            if (text.Contains("幂级数"))
            {
                if (!ContainsAny(answer, "端点", "收敛区间"))
                {
                    result.AddReason("power_series_missing_endpoint_check", "幂级数题未检查端点。", AnswerReliability.NeedsReview);
                }
            }

            if (text.Contains("泰勒"))
            {
                if (!ContainsAny(answer, "余项", "拉格朗日余项", "佩亚诺余项"))
                {
                    result.AddReason("taylor_missing_remainder", "泰勒公式题未说明余项。", AnswerReliability.NeedsReview);
                }
            }
        }

        private static bool IsFallbackOrParseFailed(string rawResponseText)
        {
            if (string.IsNullOrWhiteSpace(rawResponseText))
            {
                return false;
            }

            var text = rawResponseText.ToLowerInvariant();
            return text.Contains("llm_failed")
                || text.Contains("llm_schema_invalid")
                || text.Contains("parseerror")
                || text.Contains("json parse failed")
                || text.Contains("response_parse_failed");
        }

        private static string GetStandardSolutionText(AnalysisResultModel? parsed, AnalysisResult analysisResult)
        {
            if (parsed?.StandardSolution is { Count: > 0 })
            {
                return string.Join(" ", parsed.StandardSolution.Select(x => $"{x.Title} {x.Content}"));
            }

            try
            {
                var steps = string.IsNullOrWhiteSpace(analysisResult.StandardSolution)
                    ? new List<StandardSolutionStep>()
                    : JsonSerializer.Deserialize<List<StandardSolutionStep>>(analysisResult.StandardSolution) ?? new List<StandardSolutionStep>();

                return string.Join(" ", steps.Select(x => $"{x.Title} {x.Content}"));
            }
            catch
            {
                return string.Empty;
            }
        }

        private static List<string> ParseWarnings(string? json)
        {
            return ParseStrings(json);
        }

        private static List<string> ParseStrings(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<string>()
                    : JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            }
            catch
            {
                return new List<string>();
            }
        }

        private static bool ContainsAny(string text, params string[] fragments)
        {
            return fragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsUnclear(string? text)
        {
            return !string.IsNullOrWhiteSpace(text) && text.Contains("[unclear]", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string RemoveWhitespace(string text)
        {
            var builder = new System.Text.StringBuilder(text.Length);
            foreach (var ch in text)
            {
                if (!char.IsWhiteSpace(ch))
                {
                    builder.Append(ch);
                }
            }

            return builder.ToString();
        }

        private async Task<StructuredProblem?> LoadStructuredProblemAsync(int? structuredProblemId, CancellationToken cancellationToken)
        {
            if (!structuredProblemId.HasValue)
            {
                return null;
            }

            return await _persistenceService.GetStructuredProblemAsync(new StructuredProblemByIdQuery(structuredProblemId.Value), cancellationToken);
        }

        private async Task<PhotoSolutionOcrRecord?> LoadOcrRecordAsync(int? ocrRecordId, CancellationToken cancellationToken)
        {
            if (!ocrRecordId.HasValue)
            {
                return null;
            }

            return await _persistenceService.GetPhotoSolutionOcrRecordAsync(new PhotoSolutionOcrRecordByIdQuery(ocrRecordId.Value), cancellationToken);
        }

        private async Task<Problem?> LoadProblemAsync(int problemId, CancellationToken cancellationToken)
        {
            return await _persistenceService.GetProblemAsync(new ProblemByIdQuery(problemId), cancellationToken);
        }

        private sealed class VerificationAccumulator
        {
            public AnswerReliability AnswerReliability { get; private set; } = AnswerReliability.Reliable;
            public List<string> ReliabilityReasons { get; } = new();
            public List<string> VerifierWarnings { get; } = new();

            public void AddReason(string reasonCode, string warning, AnswerReliability severity)
            {
                ReliabilityReasons.Add(reasonCode);
                VerifierWarnings.Add(warning);
                AnswerReliability = Max(AnswerReliability, severity);
            }

            private static AnswerReliability Max(AnswerReliability left, AnswerReliability right)
            {
                return (AnswerReliability)Math.Max((int)left, (int)right);
            }
        }
    }
}
