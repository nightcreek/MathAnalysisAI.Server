using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.DTOs.PhotoSolutions;
using MathAnalysisAI.Server.SharedKernel.Analysis;
using MathAnalysisAI.Server.Services.Analysis.UAO;

namespace MathAnalysisAI.Server.Services.Analysis.Application;

public static class UAOInputModelMapper
{
    public static UAOInputModel FromRequestDto(AnalysisRequestDto dto)
    {
        return new UAOInputModel
        {
            CourseId = dto.CourseId,
            ChapterId = dto.ChapterId,
            ProblemText = dto.ProblemText,
            StudentSolutionText = dto.StudentSolutionText,
            AnalysisMode = dto.AnalysisMode,
            UserId = dto.UserId,
            OcrRecordId = dto.OcrRecordId,
            StructuredProblemId = dto.StructuredProblemId,
            Formulas = (dto.Formulas ?? new List<FormulaCandidateDto>())
                .Where(x => x != null)
                .Select(x => new FormulaCandidate
                {
                    Latex = x.Latex,
                    Context = x.Context
                })
                .ToList()
        };
    }
}
