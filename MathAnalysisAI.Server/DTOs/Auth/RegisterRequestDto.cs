using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.DTOs.Auth;

public class RegisterRequestDto
{
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? RealName { get; set; }

    [MaxLength(64)]
    public string? StudentNumber { get; set; }

    [MaxLength(128)]
    public string? SchoolName { get; set; }

    [MaxLength(128)]
    public string? DepartmentName { get; set; }

    [MaxLength(128)]
    public string? ClassName { get; set; }
}
