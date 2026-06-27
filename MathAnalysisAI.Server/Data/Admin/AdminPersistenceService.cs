using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Admin;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Data.Admin;

public sealed class AdminPersistenceService : IAdminPersistenceService
{
    private readonly ApplicationDbContext _db;

    public AdminPersistenceService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<List<UserListItemDto>> ListUsersAsync(string? search, string? role, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = BuildUserQuery(search, role);

        return await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                RealName = u.RealName,
                StudentNumber = u.StudentNumber,
                ClassName = u.ClassName,
                Role = u.Role,
                CreatedAt = u.CreatedAt,
                AnalysisCount = u.StudentSolutions.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountUsersAsync(string? search, string? role, CancellationToken cancellationToken)
    {
        return await BuildUserQuery(search, role).CountAsync(cancellationToken);
    }

    public async Task<bool> UpdateUserRoleAsync(int userId, string newRole, CancellationToken cancellationToken)
    {
        var user = await _db.AppUsers.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminDashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken)
    {
        var totalUsers = await _db.AppUsers.CountAsync(cancellationToken);
        var totalAnalyses = await _db.AnalysisResults.CountAsync(cancellationToken);
        var totalQuestions = await _db.Questions.CountAsync(cancellationToken);
        var totalOcrRecords = await _db.PhotoSolutionOcrRecords.CountAsync(cancellationToken);

        var llmStats = await _db.LLMRequestLogs
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCalls = g.LongCount(),
                SuccessCalls = g.LongCount(x => x.Status == "success"),
                FailedCalls = g.LongCount(x => x.Status != "success"),
                TotalTokens = g.Sum(x => (long?)(x.TotalTokenCount ?? 0)) ?? 0,
                AvgLatency = g.Average(x => (double?)x.LatencyMs)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        var dailyStats = await _db.AnalysisResults
            .Where(a => a.CreatedAt >= sevenDaysAgo)
            .GroupBy(a => a.CreatedAt.Date)
            .Select(g => new DailyStatDto
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                AnalysisCount = g.Count(),
                LlmCallCount = 0
            })
            .ToListAsync(cancellationToken);

        var dailyLlmCalls = await _db.LLMRequestLogs
            .Where(l => l.CreatedAt >= sevenDaysAgo)
            .GroupBy(l => l.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var dailyLlmMap = dailyLlmCalls.ToDictionary(x => x.Date, x => x.Count);
        foreach (var stat in dailyStats)
        {
            stat.LlmCallCount = dailyLlmMap.GetValueOrDefault(stat.Date, 0);
        }

        for (var i = 0; i < 7; i++)
        {
            var date = DateTime.UtcNow.Date.AddDays(-i).ToString("yyyy-MM-dd");
            if (!dailyStats.Any(d => d.Date == date))
            {
                dailyStats.Insert(0, new DailyStatDto { Date = date, AnalysisCount = 0, LlmCallCount = 0 });
            }
        }

        dailyStats.Sort((a, b) => string.CompareOrdinal(a.Date, b.Date));

        return new AdminDashboardSnapshot
        {
            TotalUsers = totalUsers,
            TotalAnalyses = totalAnalyses,
            TotalQuestions = totalQuestions,
            TotalOcrRecords = totalOcrRecords,
            TotalLlmCalls = llmStats?.TotalCalls ?? 0,
            TotalLlmSuccessCalls = llmStats?.SuccessCalls ?? 0,
            TotalLlmFailedCalls = llmStats?.FailedCalls ?? 0,
            TotalTokensConsumed = llmStats?.TotalTokens ?? 0,
            AverageLlmLatencyMs = llmStats?.AvgLatency.HasValue == true ? Math.Round((decimal)llmStats.AvgLatency.Value, 1) : 0m,
            DailyStats = dailyStats
        };
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken)
    {
        return await _db.AppUsers.AnyAsync(x => x.Username == username, cancellationToken);
    }

    public async Task<AppUser> CreateUserAsync(AppUser user, CancellationToken cancellationToken)
    {
        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);
        return user;
    }

    public async Task<bool> IsTeacherOrAdminAsync(int teacherId, CancellationToken cancellationToken)
    {
        return await _db.AppUsers.AnyAsync(
            x => x.Id == teacherId && (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin),
            cancellationToken);
    }

    public async Task AddUsersAsync(IReadOnlyCollection<AppUser> users, CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        _db.AppUsers.AddRange(users);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<TeacherListItemDto>> ListTeachersAsync(CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .Where(x => x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin)
            .OrderBy(x => x.Role == AppUserRole.Admin ? 0 : 1)
            .ThenBy(x => x.RealName)
            .Select(x => new TeacherListItemDto
            {
                Id = x.Id,
                Username = x.Username,
                RealName = x.RealName,
                Role = x.Role,
                StudentCount = x.Students.Count
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<TeacherStudentListItemDto>> ListTeacherStudentsAsync(int teacherId, CancellationToken cancellationToken)
    {
        return await _db.AppUsers
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .OrderBy(x => x.StudentNumber ?? x.RealName ?? x.Username)
            .Select(x => new TeacherStudentListItemDto
            {
                Id = x.Id,
                Username = x.Username,
                RealName = x.RealName,
                StudentNumber = x.StudentNumber,
                ClassName = x.ClassName
            })
            .ToListAsync(cancellationToken);
    }

    private IQueryable<AppUser> BuildUserQuery(string? search, string? role)
    {
        var query = _db.AppUsers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u => u.Username.ToLower().Contains(term)
                || (u.RealName != null && u.RealName.ToLower().Contains(term))
                || (u.StudentNumber != null && u.StudentNumber.ToLower().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role.Trim());
        }

        return query;
    }
}
