using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Admin;

public class AdminService
{
    private readonly IAdminPersistenceService _adminPersistenceService;

    public AdminService(IAdminPersistenceService adminPersistenceService)
    {
        _adminPersistenceService = adminPersistenceService;
    }

    public async Task<List<UserListItemDto>> ListUsersAsync(
        string? search,
        string? role,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        return await _adminPersistenceService.ListUsersAsync(search, role, page, pageSize, cancellationToken);
    }

    public async Task<int> GetUserCountAsync(string? search, string? role, CancellationToken cancellationToken = default)
    {
        return await _adminPersistenceService.CountUsersAsync(search, role, cancellationToken);
    }

    public async Task<bool> UpdateUserRoleAsync(int userId, string newRole, CancellationToken cancellationToken = default)
    {
        var validRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppUserRole.Student, AppUserRole.Teacher, AppUserRole.SchoolLeader, AppUserRole.Admin
        };

        if (!validRoles.Contains(newRole))
        {
            return false;
        }

        return await _adminPersistenceService.UpdateUserRoleAsync(userId, newRole, cancellationToken);
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var snapshot = await _adminPersistenceService.GetDashboardSnapshotAsync(cancellationToken);
        return new AdminDashboardDto
        {
            TotalUsers = snapshot.TotalUsers,
            TotalAnalyses = snapshot.TotalAnalyses,
            TotalQuestions = snapshot.TotalQuestions,
            TotalOcrRecords = snapshot.TotalOcrRecords,
            TotalLlmCalls = snapshot.TotalLlmCalls,
            TotalLlmSuccessCalls = snapshot.TotalLlmSuccessCalls,
            TotalLlmFailedCalls = snapshot.TotalLlmFailedCalls,
            TotalTokensConsumed = snapshot.TotalTokensConsumed,
            AverageLlmLatencyMs = snapshot.AverageLlmLatencyMs,
            DailyStats = snapshot.DailyStats
        };
    }

    public async Task<(bool success, string message, int? userId)> CreateTeacherAsync(
        string username,
        string password,
        string? realName,
        int bcryptWorkFactor = 12,
        CancellationToken cancellationToken = default)
    {
        var existing = await _adminPersistenceService.UsernameExistsAsync(username, cancellationToken);
        if (existing)
            return (false, "用户名已被占用。", null);

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, bcryptWorkFactor);

        var user = new AppUser
        {
            Username = username,
            PasswordHash = passwordHash,
            RealName = string.IsNullOrWhiteSpace(realName) ? username : realName.Trim(),
            Role = AppUserRole.Teacher,
            CreatedAt = DateTime.UtcNow
        };

        await _adminPersistenceService.CreateUserAsync(user, cancellationToken);

        return (true, "教师账号创建成功。", user.Id);
    }

    public async Task<(int created, int skipped, List<string> errors)> ImportStudentsAsync(
        int teacherId,
        List<StudentImportItem> students,
        int bcryptWorkFactor = 12,
        CancellationToken cancellationToken = default)
    {
        var teacherExists = await _adminPersistenceService.IsTeacherOrAdminAsync(teacherId, cancellationToken);
        if (!teacherExists)
            return (0, 0, new List<string> { "教师不存在或权限不足。" });

        var created = 0;
        var skipped = 0;
        var errors = new List<string>();
        var pendingUsers = new List<AppUser>();

        foreach (var item in students)
        {
            if (string.IsNullOrWhiteSpace(item.RealName) && string.IsNullOrWhiteSpace(item.StudentNumber))
            {
                skipped++;
                continue;
            }

            var username = !string.IsNullOrWhiteSpace(item.StudentNumber)
                ? item.StudentNumber.Trim()
                : (item.RealName ?? "").Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                skipped++;
                continue;
            }

            var existing = await _adminPersistenceService.UsernameExistsAsync(username, cancellationToken);
            if (existing)
            {
                skipped++;
                continue;
            }

            var defaultPassword = !string.IsNullOrWhiteSpace(item.StudentNumber)
                ? item.StudentNumber.Trim()
                : username;
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword, bcryptWorkFactor);

            var user = new AppUser
            {
                Username = username,
                PasswordHash = passwordHash,
                RealName = item.RealName?.Trim() ?? username,
                StudentNumber = item.StudentNumber?.Trim(),
                Role = AppUserRole.Student,
                TeacherId = teacherId,
                CreatedAt = DateTime.UtcNow
            };

            pendingUsers.Add(user);
            created++;
        }

        await _adminPersistenceService.AddUsersAsync(pendingUsers, cancellationToken);
        return (created, skipped, errors);
    }

    public async Task<List<TeacherListItemDto>> ListTeachersAsync(CancellationToken cancellationToken = default)
    {
        return await _adminPersistenceService.ListTeachersAsync(cancellationToken);
    }

    public async Task<List<TeacherStudentListItemDto>> ListTeacherStudentsAsync(
        int teacherId,
        CancellationToken cancellationToken = default)
    {
        return await _adminPersistenceService.ListTeacherStudentsAsync(teacherId, cancellationToken);
    }
}
