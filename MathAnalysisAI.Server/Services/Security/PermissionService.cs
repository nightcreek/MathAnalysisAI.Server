using MathAnalysisAI.Server.Services.Auth;

namespace MathAnalysisAI.Server.Services.Security
{
    public class PermissionService
    {
        private readonly IIdentityKernel _identityKernel;

        public PermissionService(IIdentityKernel identityKernel)
        {
            _identityKernel = identityKernel;
        }

        public async Task<bool> CanViewRealStudentInfoAsync(
            int viewerUserId,
            int targetStudentUserId,
            int courseId,
            CancellationToken cancellationToken = default)
            => await _identityKernel.CanViewRealStudentInfoAsync(viewerUserId, targetStudentUserId, courseId, cancellationToken);

        public async Task<bool> CanViewCourseLeaderboardWithRealNamesAsync(
            int viewerUserId,
            int courseId,
            CancellationToken cancellationToken = default)
            => await _identityKernel.CanViewCourseLeaderboardWithRealNamesAsync(viewerUserId, courseId, cancellationToken);
    }
}
