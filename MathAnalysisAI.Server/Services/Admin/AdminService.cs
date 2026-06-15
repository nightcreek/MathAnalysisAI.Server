using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Models;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Services.Admin;

public class AdminService
{
    private readonly ApplicationDbContext _db;

    public AdminService(ApplicationDbContext db)
    {
        _db = db;
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

        var query = _db.AppUsers.AsNoTracking();

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

        var users = await query
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

        return users;
    }

    public async Task<int> GetUserCountAsync(string? search, string? role, CancellationToken cancellationToken = default)
    {
        var query = _db.AppUsers.AsNoTracking();

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

        return await query.CountAsync(cancellationToken);
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

        var user = await _db.AppUsers.FindAsync(new object[] { userId }, cancellationToken);
        if (user == null)
        {
            return false;
        }

        user.Role = newRole;
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken cancellationToken = default)
    {
        var totalUsers = await _db.AppUsers.CountAsync(cancellationToken);
        var totalAnalyses = await _db.AnalysisResults.CountAsync(cancellationToken);
        var totalQuestions = await _db.Questions.CountAsync(cancellationToken);
        var totalOcrRecords = await _db.PhotoSolutionOcrRecords.CountAsync(cancellationToken);

        var llmStats = await _db.LLMRequestLogs
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalCalls = g.Count(),
                SuccessCalls = g.Count(x => x.Status == "success"),
                FailedCalls = g.Count(x => x.Status != "success"),
                TotalTokens = g.Sum(x => x.TotalTokenCount ?? 0),
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

        return new AdminDashboardDto
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

    public async Task<(bool success, string message, int? userId)> CreateTeacherAsync(
        string username,
        string password,
        string? realName,
        int bcryptWorkFactor = 12,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.AppUsers
            .AnyAsync(x => x.Username == username, cancellationToken);
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

        _db.AppUsers.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        return (true, "教师账号创建成功。", user.Id);
    }

    public async Task<(int created, int skipped, List<string> errors)> ImportStudentsAsync(
        int teacherId,
        List<StudentImportItem> students,
        int bcryptWorkFactor = 12,
        CancellationToken cancellationToken = default)
    {
        var teacher = await _db.AppUsers
            .FirstOrDefaultAsync(x => x.Id == teacherId &&
                (x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin), cancellationToken);

        if (teacher == null)
            return (0, 0, new List<string> { "教师不存在或权限不足。" });

        var created = 0;
        var skipped = 0;
        var errors = new List<string>();

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

            var existing = await _db.AppUsers.AnyAsync(x => x.Username == username, cancellationToken);
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

            _db.AppUsers.Add(user);
            created++;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (created, skipped, errors);
    }
}
