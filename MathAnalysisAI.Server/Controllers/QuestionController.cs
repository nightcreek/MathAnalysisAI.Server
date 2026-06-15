using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/questions")]
[RequireAuth]
public class QuestionController : ControllerBase
{
    private readonly QuestionService _questionService;

    public QuestionController(QuestionService questionService)
    {
        _questionService = questionService;
    }

    [HttpGet]
    public async Task<ActionResult<QuestionListResponseDto>> List(
        [FromQuery] int? courseId,
        [FromQuery] int? chapterId,
        [FromQuery] int? knowledgePointId,
        [FromQuery] string? difficulty,
        [FromQuery] string? questionType,
        [FromQuery] string? search,
        [FromQuery] bool publishedOnly = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _questionService.ListAsync(
            courseId, chapterId, knowledgePointId, difficulty, questionType,
            search, publishedOnly, page, pageSize, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuestionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var question = await _questionService.GetByIdAsync(id, cancellationToken);
        if (question == null)
            return NotFound();

        return Ok(question);
    }

    [HttpPost]
    public async Task<ActionResult<QuestionDto>> Create(
        [FromBody] CreateQuestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = HttpContext.GetCurrentUserId();
        var question = await _questionService.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = question.Id }, question);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuestionDto>> Update(
        int id,
        [FromBody] CreateQuestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var question = await _questionService.UpdateAsync(id, request, cancellationToken);
        if (question == null)
            return NotFound();

        return Ok(question);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _questionService.DeleteAsync(id, cancellationToken);
        if (!deleted)
            return NotFound();

        return NoContent();
    }
}
