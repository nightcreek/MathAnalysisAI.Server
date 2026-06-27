using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Analysis.Mistakes
{
    public sealed class MistakeRecordService : IMistakeRecordService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly ILogger<MistakeRecordService> _logger;

        public MistakeRecordService(
            IPersistenceService persistenceService,
            ILogger<MistakeRecordService> logger)
        {
            _persistenceService = persistenceService;
            _logger = logger;
        }

        public async Task<List<int>> SaveMistakeRecordsAsync(
            int analysisResultId,
            int courseId,
            IReadOnlyList<string> normalizedKnowledgePointCodes,
            IReadOnlyList<string> mistakeTags,
            CancellationToken cancellationToken)
        {
            if (analysisResultId <= 0 || mistakeTags == null || mistakeTags.Count == 0)
            {
                return new List<int>();
            }

            var codes = (normalizedKnowledgePointCodes ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var knowledgePointIdMap = await LoadKnowledgePointIdMapAsync(
                courseId,
                codes,
                cancellationToken);

            var primaryKnowledgePointId = codes
                .Select(code => knowledgePointIdMap.TryGetValue(code, out var id) ? id : (int?)null)
                .FirstOrDefault(id => id.HasValue);

            _logger.LogDebug(
                "MistakeRecord binding: AnalysisResultId={AnalysisResultId}, CourseId={CourseId}, Codes=[{Codes}], MapCount={MapCount}, PrimaryKnowledgePointId={PrimaryKnowledgePointId}",
                analysisResultId,
                courseId,
                string.Join(",", codes),
                knowledgePointIdMap.Count,
                primaryKnowledgePointId);

            var records = mistakeTags
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.Ordinal)
                .Select(tag =>
                {
                    var resolvedKnowledgePointId = ResolveMistakeKnowledgePointId(
                        tag,
                        codes,
                        knowledgePointIdMap,
                        primaryKnowledgePointId);

                    _logger.LogDebug(
                        "MistakeRecord binding detail: AnalysisResultId={AnalysisResultId}, MistakeTag={MistakeTag}, ResolvedKnowledgePointId={ResolvedKnowledgePointId}",
                        analysisResultId,
                        tag,
                        resolvedKnowledgePointId);

                    return new MistakeRecord
                    {
                        AnalysisResultId = analysisResultId,
                        KnowledgePointId = resolvedKnowledgePointId,
                        MistakeTag = tag,
                        Description = null,
                        Severity = 1,
                        CreatedAt = DateTime.UtcNow
                    };
                })
                .ToList();

            if (records.Count == 0)
            {
                return new List<int>();
            }

            await _persistenceService.AddMistakeRecordsAsync(new AddMistakeRecordsCommand(records), cancellationToken);
            return records
                .Where(x => x.KnowledgePointId.HasValue && x.KnowledgePointId.Value > 0)
                .Select(x => x.KnowledgePointId!.Value)
                .Distinct()
                .ToList();
        }

        private async Task<Dictionary<string, int>> LoadKnowledgePointIdMapAsync(
            int courseId,
            IReadOnlyList<string> knowledgePointCodes,
            CancellationToken cancellationToken)
        {
            if (courseId <= 0 || knowledgePointCodes == null || knowledgePointCodes.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var normalizedCodes = knowledgePointCodes
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalizedCodes.Count == 0)
            {
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }

            var requestedSet = new HashSet<string>(
                normalizedCodes,
                StringComparer.OrdinalIgnoreCase);

            var map = (await _persistenceService.GetKnowledgePointsByCodesAsync(
                    new KnowledgePointsByCodesQuery(courseId, requestedSet.ToList()),
                    cancellationToken))
                .Where(kp => !string.IsNullOrWhiteSpace(kp.Code))
                .ToDictionary(kp => kp.Code!, kp => kp.Id, StringComparer.OrdinalIgnoreCase);

            return map;
        }

        private static int? ResolveMistakeKnowledgePointId(
            string tag,
            IReadOnlyList<string> normalizedKnowledgePointCodes,
            IReadOnlyDictionary<string, int> knowledgePointIdMap,
            int? primaryKnowledgePointId)
        {
            int? comparisonTestId = null;
            if (knowledgePointIdMap.TryGetValue("ma.improper_integral.comparison_test", out var compId))
            {
                comparisonTestId = compId;
            }

            var value = tag?.Trim() ?? string.Empty;
            if (comparisonTestId.HasValue
                && (value.Contains("比较判别", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("判别法", StringComparison.OrdinalIgnoreCase)))
            {
                return comparisonTestId.Value;
            }

            return primaryKnowledgePointId;
        }
    }
}
