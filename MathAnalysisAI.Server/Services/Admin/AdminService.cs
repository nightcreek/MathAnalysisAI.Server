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
}
