using MathAnalysisAI.Server.DTOs.Analysis;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.Knowledge;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/questions")]
[Authorize(Policy = AuthPolicies.AuthenticatedUser)]
public class QuestionController : ControllerBase
{
    private readonly QuestionService _questionService;
    private readonly IUserContext _userContext;

    public QuestionController(QuestionService questionService, IUserContext userContext)
    {
        _questionService = questionService;
        _userContext = userContext;
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

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        var canManageQuestions = currentUser != null &&
                                 (string.Equals(currentUser.Role, AppUserRole.Teacher, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(currentUser.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase));

        var result = await _questionService.ListAsync(
            courseId,
            chapterId,
            knowledgePointId,
            difficulty,
            questionType,
            search,
            canManageQuestions ? publishedOnly : true,
            page,
            pageSize,
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuestionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        var canManageQuestions = currentUser != null &&
                                 (string.Equals(currentUser.Role, AppUserRole.Teacher, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(currentUser.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase));

        var question = await _questionService.GetByIdAsync(id, cancellationToken);
        if (question == null || (!question.IsPublished && !canManageQuestions))
        {
            return NotFound();
        }

        return Ok(question);
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult<QuestionDto>> Create(
        [FromBody] CreateQuestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var userId = await _userContext.GetCurrentUserIdAsync(cancellationToken);
        var question = await _questionService.CreateAsync(request, userId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = question.Id }, question);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult<QuestionDto>> Update(
        int id,
        [FromBody] CreateQuestionRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var question = await _questionService.UpdateAsync(id, request, cancellationToken);
        if (question == null)
        {
            return NotFound();
        }

        return Ok(question);
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var deleted = await _questionService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        return NoContent();
    }
}
