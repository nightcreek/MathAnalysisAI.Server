using MathAnalysisAI.Server.DTOs.Leaderboard;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Security;

namespace MathAnalysisAI.Server.Services.Ranking
{
    public class LeaderboardService
    {
        private readonly IPersistenceService _persistenceService;
        private readonly PermissionService _permissionService;

        public LeaderboardService(IPersistenceService persistenceService, PermissionService permissionService)
        {
            _persistenceService = persistenceService;
            _permissionService = permissionService;
        }

        public async Task<List<LeaderboardEntryDto>> GetPublicLeaderboardAsync(
            int courseId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            if (courseId <= 0)
            {
                return new List<LeaderboardEntryDto>();
            }

            limit = NormalizeLimit(limit);

            var rows = await _persistenceService.GetLeaderboardUserCourseStatsAsync(
                new LeaderboardQuery(courseId, limit),
                cancellationToken);

            return rows.Select((x, index) => new LeaderboardEntryDto
            {
                Rank = index + 1,
                UserId = x.UserId,
                Username = x.User?.Username ?? string.Empty,
                CourseId = x.CourseId,
                CourseName = x.Course?.Name ?? string.Empty,
                AttemptCount = x.AttemptCount,
                CorrectCount = x.CorrectCount,
                WrongCount = x.WrongCount,
                AccuracyRate = x.AccuracyRate,
                RankingScore = x.RankingScore
            }).ToList();
        }

        public async Task<List<TeacherLeaderboardEntryDto>> GetTeacherLeaderboardAsync(
            int viewerUserId,
            int courseId,
            int limit,
            CancellationToken cancellationToken = default)
        {
            if (viewerUserId <= 0 || courseId <= 0)
            {
                return new List<TeacherLeaderboardEntryDto>();
            }

            limit = NormalizeLimit(limit);

            var canViewRealNames = await _permissionService
                .CanViewCourseLeaderboardWithRealNamesAsync(viewerUserId, courseId, cancellationToken);

            var rows = await _persistenceService.GetLeaderboardUserCourseStatsAsync(
                new LeaderboardQuery(courseId, limit),
                cancellationToken);

            return rows.Select((x, index) => new TeacherLeaderboardEntryDto
            {
                Rank = index + 1,
                UserId = x.UserId,
                Username = x.User?.Username ?? string.Empty,
                CourseId = x.CourseId,
                CourseName = x.Course?.Name ?? string.Empty,
                AttemptCount = x.AttemptCount,
                CorrectCount = x.CorrectCount,
                WrongCount = x.WrongCount,
                AccuracyRate = x.AccuracyRate,
                RankingScore = x.RankingScore,
                RealName = canViewRealNames ? x.User?.RealName : null,
                StudentNumber = canViewRealNames ? x.User?.StudentNumber : null,
                ClassName = canViewRealNames ? x.User?.ClassName : null
            }).ToList();
        }

        private static int NormalizeLimit(int limit)
        {
            if (limit <= 0)
            {
                return 20;
            }

            return Math.Min(limit, 200);
        }
    }
}
