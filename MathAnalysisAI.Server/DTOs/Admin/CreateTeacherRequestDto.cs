using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.DTOs.Admin;

public class CreateTeacherRequestDto
{
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? RealName { get; set; }
}
