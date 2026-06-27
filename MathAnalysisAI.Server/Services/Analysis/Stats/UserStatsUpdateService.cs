using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;

namespace MathAnalysisAI.Server.Services.Analysis.Stats
{
    public sealed class UserStatsUpdateService : IUserStatsUpdateService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly ILogger<UserStatsUpdateService> _logger;

        public UserStatsUpdateService(
            IPersistenceService persistenceService,
            ILogger<UserStatsUpdateService> logger)
        {
            _persistenceService = persistenceService;
            _logger = logger;
        }

        public bool? ResolveCorrectness(AnalysisResult? analysisResult)
        {
            if (analysisResult == null)
            {
                return null;
            }

            if (analysisResult.FinalCorrect.HasValue)
            {
                return analysisResult.FinalCorrect.Value;
            }

            if (analysisResult.AiJudgedCorrect.HasValue)
            {
                return analysisResult.AiJudgedCorrect.Value;
            }

            return null;
        }

        public async Task UpdateAfterAnalysisAsync(
            int? requestUserId,
            int? studentSolutionUserId,
            int courseId,
            IReadOnlyList<string> normalizedKnowledgePointCodes,
            IReadOnlyList<int> mistakeKnowledgePointIds,
            AnalysisResult analysisResult,
            CancellationToken cancellationToken)
        {
            var effectiveUserId = requestUserId ?? studentSolutionUserId;
            var correctness = ResolveCorrectness(analysisResult);

            await UpdateCourseStatsAsync(
                effectiveUserId,
                courseId,
                correctness,
                cancellationToken);

            await UpdateUserKnowledgeStateAsync(
                effectiveUserId,
                courseId,
                normalizedKnowledgePointCodes,
                mistakeKnowledgePointIds,
                analysisResult,
                cancellationToken);
        }

        public async Task UpdateCourseStatsAsync(
            int? userId,
            int courseId,
            bool? isCorrect,
            CancellationToken cancellationToken)
        {
            if (!userId.HasValue || userId.Value <= 0 || courseId <= 0)
            {
                return;
            }

            var stats = await _persistenceService.GetUserCourseStatsAsync(
                new UserCourseStatsByKeyQuery(userId.Value, courseId),
                cancellationToken);

            if (stats == null)
            {
                stats = new UserCourseStats
                {
                    UserId = userId.Value,
                    CourseId = courseId,
                    AttemptCount = 0,
                    CorrectCount = 0,
                    WrongCount = 0,
                    AccuracyRate = 0m,
                    RankingScore = 0m,
                    LastUpdatedAt = DateTime.UtcNow
                };

            }

            stats.AttemptCount += 1;
            if (isCorrect == true)
            {
                stats.CorrectCount += 1;
            }
            else if (isCorrect == false)
            {
                stats.WrongCount += 1;
            }

            var accuracy = stats.AttemptCount == 0
                ? 0m
                : (decimal)stats.CorrectCount / stats.AttemptCount * 100m;

            var ranking = (double)accuracy * Math.Log(stats.AttemptCount + 1);

            stats.AccuracyRate = decimal.Round(accuracy, 2, MidpointRounding.AwayFromZero);
            stats.RankingScore = decimal.Round((decimal)ranking, 4, MidpointRounding.AwayFromZero);
            stats.LastUpdatedAt = DateTime.UtcNow;

            await _persistenceService.SaveUserCourseStatsAsync(
                new SaveUserCourseStatsCommand(stats),
                cancellationToken);
        }

        private async Task UpdateUserKnowledgeStateAsync(
            int? userId,
            int courseId,
            IReadOnlyList<string>? normalizedKnowledgePointCodes,
            IReadOnlyList<int>? mistakeKnowledgePointIds,
            AnalysisResult analysisResult,
            CancellationToken cancellationToken)
        {
            if (!userId.HasValue || userId.Value <= 0 || courseId <= 0)
            {
                _logger.LogDebug(
                    "UserKnowledgeState update skipped: no effective user id. UserId={UserId}, CourseId={CourseId}",
                    userId,
                    courseId);
                return;
            }

            var correctness = ResolveCorrectness(analysisResult);
            if (!correctness.HasValue)
            {
                _logger.LogDebug(
                    "UserKnowledgeState update skipped: correctness is null. UserId={UserId}, CourseId={CourseId}, AnalysisResultId={AnalysisResultId}",
                    userId.Value,
                    courseId,
                    analysisResult.Id);
                return;
            }

            var targetKnowledgePointIds = new HashSet<int>();

            if (correctness.Value)
            {
                var normalizedCodes = (normalizedKnowledgePointCodes ?? Array.Empty<string>())
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (normalizedCodes.Count == 0)
                {
                    _logger.LogDebug(
                        "UserKnowledgeState update skipped: no normalized knowledge point codes for correct result. UserId={UserId}, CourseId={CourseId}, AnalysisResultId={AnalysisResultId}",
                        userId.Value,
                        courseId,
                        analysisResult.Id);
                    return;
                }

                _logger.LogDebug(
                    "UserKnowledgeState correct target codes: UserId={UserId}, CourseId={CourseId}, AnalysisResultId={AnalysisResultId}, Codes=[{Codes}]",
                    userId.Value,
                    courseId,
                    analysisResult.Id,
                    string.Join(",", normalizedCodes));

                var knowledgePointIds = (await _persistenceService.GetKnowledgePointsByCodesAsync(
                        new KnowledgePointsByCodesQuery(courseId, normalizedCodes),
                        cancellationToken))
                    .Select(x => x.Id)
                    .ToList();

                foreach (var id in knowledgePointIds)
                {
                    targetKnowledgePointIds.Add(id);
                }
            }
            else
            {
                _logger.LogDebug(
                    "UserKnowledgeState mistake target ids: UserId={UserId}, CourseId={CourseId}, AnalysisResultId={AnalysisResultId}, Ids=[{Ids}]",
                    userId.Value,
                    courseId,
                    analysisResult.Id,
                    string.Join(",", mistakeKnowledgePointIds ?? Array.Empty<int>()));

                foreach (var id in mistakeKnowledgePointIds ?? Array.Empty<int>())
                {
                    if (id > 0)
                    {
                        targetKnowledgePointIds.Add(id);
                    }
                }
            }

            if (targetKnowledgePointIds.Count == 0)
            {
                _logger.LogDebug(
                    "UserKnowledgeState update skipped: target knowledge point ids empty. UserId={UserId}, CourseId={CourseId}, AnalysisResultId={AnalysisResultId}",
                    userId.Value,
                    courseId,
                    analysisResult.Id);
                return;
            }

            var targetIds = targetKnowledgePointIds.ToList();
            var existingStates = await _persistenceService.GetUserKnowledgeStatesAsync(
                new UserKnowledgeStatesQuery(userId.Value, targetIds),
                cancellationToken);

            var stateMap = existingStates.ToDictionary(x => x.KnowledgePointId, x => x);
            var now = DateTime.UtcNow;

            foreach (var knowledgePointId in targetIds)
            {
                if (!stateMap.TryGetValue(knowledgePointId, out var state))
                {
                    state = new UserKnowledgeState
                    {
                        UserId = userId.Value,
                        KnowledgePointId = knowledgePointId,
                        PracticeCount = 0,
                        CorrectCount = 0,
                        MasteryLevel = 0,
                        LastUpdatedAt = now
                    };

                    stateMap[knowledgePointId] = state;
                }

                state.PracticeCount += 1;
                if (correctness.Value)
                {
                    state.CorrectCount += 1;
                }

                state.MasteryLevel = CalculateMasteryLevel(state.CorrectCount, state.PracticeCount);
                state.LastUpdatedAt = now;

                _logger.LogDebug(
                    "UserKnowledgeState updated: UserId={UserId}, KnowledgePointId={KnowledgePointId}, PracticeCount={PracticeCount}, CorrectCount={CorrectCount}, MasteryLevel={MasteryLevel}",
                    userId.Value,
                    knowledgePointId,
                    state.PracticeCount,
                    state.CorrectCount,
                    state.MasteryLevel);
            }

            await _persistenceService.SaveUserKnowledgeStatesAsync(
                new SaveUserKnowledgeStatesCommand(stateMap.Values.ToList()),
                cancellationToken);
        }

        private static int CalculateMasteryLevel(int correctCount, int practiceCount)
        {
            if (practiceCount <= 0 || correctCount <= 0)
            {
                return 0;
            }

            var ratio = (double)correctCount / practiceCount * 100d;
            var rounded = (int)Math.Round(ratio, 0, MidpointRounding.AwayFromZero);
            return Math.Clamp(rounded, 0, 100);
        }
    }
}
