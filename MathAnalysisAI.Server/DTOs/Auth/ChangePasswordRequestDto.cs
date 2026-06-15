using System.ComponentModel.DataAnnotations;

namespace MathAnalysisAI.Server.DTOs.Auth;

public class ChangePasswordRequestDto
{
    [Required]
    [MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}
