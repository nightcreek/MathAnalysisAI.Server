using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Models;

namespace MathAnalysisAI.Server.Services.Admin;

public interface IAdminPersistenceService
{
    Task<List<UserListItemDto>> ListUsersAsync(string? search, string? role, int page, int pageSize, CancellationToken cancellationToken);
    Task<int> CountUsersAsync(string? search, string? role, CancellationToken cancellationToken);
    Task<bool> UpdateUserRoleAsync(int userId, string newRole, CancellationToken cancellationToken);
    Task<AdminDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken);
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken);
    Task<AppUser> CreateUserAsync(AppUser user, CancellationToken cancellationToken);
    Task<bool> IsTeacherOrAdminAsync(int teacherId, CancellationToken cancellationToken);
    Task AddUsersAsync(IReadOnlyCollection<AppUser> users, CancellationToken cancellationToken);
    Task<List<TeacherListItemDto>> ListTeachersAsync(CancellationToken cancellationToken);
    Task<List<TeacherStudentListItemDto>> ListTeacherStudentsAsync(int teacherId, CancellationToken cancellationToken);
}

public sealed class AdminDashboardSnapshot
{
    public int TotalUsers { get; init; }
    public int TotalAnalyses { get; init; }
    public int TotalQuestions { get; init; }
    public int TotalOcrRecords { get; init; }
    public long TotalLlmCalls { get; init; }
    public long TotalLlmSuccessCalls { get; init; }
    public long TotalLlmFailedCalls { get; init; }
    public long TotalTokensConsumed { get; init; }
    public decimal AverageLlmLatencyMs { get; init; }
    public List<DailyStatDto> DailyStats { get; init; } = new();
}
