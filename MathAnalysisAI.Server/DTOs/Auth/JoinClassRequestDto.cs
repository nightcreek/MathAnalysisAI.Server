namespace MathAnalysisAI.Server.DTOs.Auth;

public class JoinClassRequestDto
{
    public string? TeacherId { get; set; }
    public string? TeacherUsername { get; set; }
    public string? RealName { get; set; }
    public string? StudentNumber { get; set; }
    public string? SchoolName { get; set; }
    public string? ClassName { get; set; }
}
