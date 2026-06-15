namespace MathAnalysisAI.Server.DTOs.Admin;

public class ImportStudentsRequestDto
{
    public int TeacherId { get; set; }
    public List<StudentImportItem> Students { get; set; } = new();
}

public class StudentImportItem
{
    public string? RealName { get; set; }
    public string? StudentNumber { get; set; }
}
