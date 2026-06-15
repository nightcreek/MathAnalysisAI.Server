using System.Text.Json;
using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Analysis.Structuring
{
    public sealed class ProblemStructuringService : IProblemStructuringService
    {
        private static readonly string[] TargetKeywords =
        {
            "求", "证明", "求证", "判断", "讨论", "计算", "说明"
        };

        private readonly ApplicationDbContext _db;

        public ProblemStructuringService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<StructuredProblem> CreateFromManualInputAsync(
            AnalysisRequestDto request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var rawProblemText = NormalizeText(request.ProblemText);
            var normalizedProblemText = NormalizeWhitespace(rawProblemText);
            var formulas = NormalizeFormulas(request.Formulas);
            var sourceType = formulas.Count > 0
                ? StructuredProblemSourceType.MathLive
                : StructuredProblemSourceType.Manual;

            var entity = new StructuredProblem
            {
                SourceType = sourceType,
                RawProblemText = rawProblemText,
                NormalizedProblemText = normalizedProblemText,
                StudentSolutionText = NormalizeNullableText(request.StudentSolutionText),
                FormulasJson = JsonSerializer.Serialize(formulas),
                GivenConditionsJson = JsonSerializer.Serialize(ExtractGivenConditions(normalizedProblemText)),
                TargetText = ExtractTargetText(normalizedProblemText),
                ProblemType = ClassifyProblemType(normalizedProblemText),
                KnowledgePointCandidatesJson = "[]",
                Confidence = 1.0m,
                Status = DetermineStatus(normalizedProblemText),
                PhotoSolutionOcrRecordId = request.OcrRecordId,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.StructuredProblems.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        public async Task<StructuredProblem> CreateFromConfirmedOcrAsync(
            PhotoSolutionOcrRecord ocrRecord,
            AnalysisRequestDto request,
            int userId,
            CancellationToken cancellationToken = default)
        {
            var confirmedProblemText = NormalizeText(ocrRecord.ConfirmedProblemText ?? request.ProblemText);
            var normalizedProblemText = NormalizeWhitespace(confirmedProblemText);
            var studentSolutionText = NormalizeNullableText(ocrRecord.ConfirmedStudentSolutionText ?? request.StudentSolutionText);
            var formulas = ParseFormulas(ocrRecord.ConfirmedFormulasJson);

            var entity = new StructuredProblem
            {
                SourceType = StructuredProblemSourceType.OCR,
                RawProblemText = NormalizeText(ocrRecord.RecognizedProblemText ?? confirmedProblemText),
                NormalizedProblemText = normalizedProblemText,
                StudentSolutionText = studentSolutionText,
                FormulasJson = JsonSerializer.Serialize(formulas),
                GivenConditionsJson = JsonSerializer.Serialize(ExtractGivenConditions(normalizedProblemText)),
                TargetText = ExtractTargetText(normalizedProblemText),
                ProblemType = ClassifyProblemType(normalizedProblemText),
                KnowledgePointCandidatesJson = "[]",
                Confidence = ocrRecord.Confidence,
                Status = DetermineStatus(normalizedProblemText),
                PhotoSolutionOcrRecordId = ocrRecord.Id,
                CreatedByUserId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.StructuredProblems.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);
            return entity;
        }

        private static StructuredProblemStatus DetermineStatus(string normalizedProblemText)
        {
            if (string.IsNullOrWhiteSpace(normalizedProblemText) || normalizedProblemText.Length < 8)
            {
                return StructuredProblemStatus.NeedsReview;
            }

            return StructuredProblemStatus.Structured;
        }

        private static string NormalizeText(string? value)
        {
            return value?.Trim() ?? string.Empty;
        }

        private static string? NormalizeNullableText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            return value.Trim();
        }

        private static string NormalizeWhitespace(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        private static List<FormulaCandidateDto> NormalizeFormulas(IEnumerable<FormulaCandidateDto>? formulas)
        {
            return (formulas ?? Enumerable.Empty<FormulaCandidateDto>())
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Latex))
                .Select(x => new FormulaCandidateDto
                {
                    Latex = x.Latex.Trim(),
                    Context = string.IsNullOrWhiteSpace(x.Context) ? null : x.Context.Trim()
                })
                .ToList();
        }

        private static List<FormulaCandidateDto> ParseFormulas(string? json)
        {
            try
            {
                return string.IsNullOrWhiteSpace(json)
                    ? new List<FormulaCandidateDto>()
                    : JsonSerializer.Deserialize<List<FormulaCandidateDto>>(json) ?? new List<FormulaCandidateDto>();
            }
            catch
            {
                return new List<FormulaCandidateDto>();
            }
        }

        private static List<string> ExtractGivenConditions(string normalizedProblemText)
        {
            var targetText = ExtractTargetText(normalizedProblemText);
            if (string.IsNullOrWhiteSpace(targetText))
            {
                return new List<string>();
            }

            var index = normalizedProblemText.IndexOf(targetText, StringComparison.Ordinal);
            if (index <= 0)
            {
                return new List<string>();
            }

            var leading = normalizedProblemText[..index].Trim();
            return string.IsNullOrWhiteSpace(leading) ? new List<string>() : new List<string> { leading };
        }

        private static string? ExtractTargetText(string normalizedProblemText)
        {
            if (string.IsNullOrWhiteSpace(normalizedProblemText))
            {
                return null;
            }

            var keyword = TargetKeywords
                .Select(k => new { Keyword = k, Index = normalizedProblemText.IndexOf(k, StringComparison.Ordinal) })
                .Where(x => x.Index >= 0)
                .OrderBy(x => x.Index)
                .FirstOrDefault();

            if (keyword == null)
            {
                return null;
            }

            var tail = normalizedProblemText[keyword.Index..].Trim();
            return string.IsNullOrWhiteSpace(tail) ? null : tail;
        }

        private static string? ClassifyProblemType(string normalizedProblemText)
        {
            if (string.IsNullOrWhiteSpace(normalizedProblemText))
            {
                return "unknown";
            }

            if (normalizedProblemText.Contains("极限", StringComparison.Ordinal))
            {
                return "limit";
            }

            if (normalizedProblemText.Contains("连续", StringComparison.Ordinal))
            {
                return "continuity";
            }

            if (normalizedProblemText.Contains("导数", StringComparison.Ordinal) || normalizedProblemText.Contains("微分", StringComparison.Ordinal))
            {
                return "differential";
            }

            if (normalizedProblemText.Contains("积分", StringComparison.Ordinal))
            {
                return "integral";
            }

            if (normalizedProblemText.Contains("级数", StringComparison.Ordinal))
            {
                return "series";
            }

            if (normalizedProblemText.Contains("多元", StringComparison.Ordinal))
            {
                return "multivariable";
            }

            return "unknown";
        }
    }
}
