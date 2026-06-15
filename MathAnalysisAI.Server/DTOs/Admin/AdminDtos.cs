namespace MathAnalysisAI.Server.DTOs.Admin;

public class UserListItemDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? RealName { get; set; }
    public string? StudentNumber { get; set; }
    public string? ClassName { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int AnalysisCount { get; set; }
}

public class UpdateUserRoleRequestDto
{
    public string Role { get; set; } = string.Empty;
}

public class AdminDashboardDto
{
    public int TotalUsers { get; set; }
    public int TotalAnalyses { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalOcrRecords { get; set; }
    public long TotalLlmCalls { get; set; }
    public long TotalLlmSuccessCalls { get; set; }
    public long TotalLlmFailedCalls { get; set; }
    public long TotalTokensConsumed { get; set; }
    public decimal AverageLlmLatencyMs { get; set; }
    public List<DailyStatDto> DailyStats { get; set; } = new();
}

public class DailyStatDto
{
    public string Date { get; set; } = string.Empty;
    public int AnalysisCount { get; set; }
    public int LlmCallCount { get; set; }
}
