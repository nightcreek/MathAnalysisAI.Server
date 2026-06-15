using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.DTOs.Auth;

public class LoginRequestDto
{
    [Required]
    [MaxLength(64)]
    public string Username { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? Password { get; set; }
}
