namespace MathAnalysisAI.Server.DTOs.PhotoSolutions
{
    public class ConfirmPhotoSolutionOcrRequestDto
    {
        public string ProblemText { get; set; } = string.Empty;
        public string? StudentSolutionText { get; set; }
        public List<FormulaCandidateDto> Formulas { get; set; } = new();
    }
}
