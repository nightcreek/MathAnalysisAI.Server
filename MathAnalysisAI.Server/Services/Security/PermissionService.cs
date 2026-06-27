using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Security
{
    public class PermissionService
    {
        private readonly IPermissionPersistenceService _permissionPersistenceService;

        public PermissionService(IPermissionPersistenceService permissionPersistenceService)
        {
            _permissionPersistenceService = permissionPersistenceService;
        }

        public async Task<bool> CanViewRealStudentInfoAsync(
            int viewerUserId,
            int targetStudentUserId,
            int courseId,
            CancellationToken cancellationToken = default)
        {
            if (viewerUserId <= 0 || targetStudentUserId <= 0 || courseId <= 0)
            {
                return false;
            }

            var viewer = await _permissionPersistenceService.FindUserByIdAsync(viewerUserId, cancellationToken);

            if (viewer == null)
            {
                return false;
            }

            if (viewer.Role == AppUserRole.Admin || viewer.Role == AppUserRole.SchoolLeader)
            {
                return true;
            }

            // Phase-1 temporary rule: teacher can view real info for the requested course.
            // TODO: replace with TeacherCourseAssignment/CourseEnrollment relation checks.
            if (viewer.Role == AppUserRole.Teacher)
            {
                return true;
            }

            // student can only view own real info.
            if (viewer.Role == AppUserRole.Student && viewerUserId == targetStudentUserId)
            {
                return true;
            }

            return false;
        }

        public async Task<bool> CanViewCourseLeaderboardWithRealNamesAsync(
            int viewerUserId,
            int courseId,
            CancellationToken cancellationToken = default)
        {
            if (viewerUserId <= 0 || courseId <= 0)
            {
                return false;
            }

            var viewer = await _permissionPersistenceService.FindUserByIdAsync(viewerUserId, cancellationToken);

            if (viewer == null)
            {
                return false;
            }

            if (viewer.Role == AppUserRole.Admin || viewer.Role == AppUserRole.SchoolLeader)
            {
                return true;
            }

            // Phase-1 temporary rule: teacher can view real-name leaderboard for requested course.
            // TODO: replace with TeacherCourseAssignment relation checks.
            if (viewer.Role == AppUserRole.Teacher)
            {
                return true;
            }

            return false;
        }
    }
}
