using MathAnalysisAI.Server.DTOs.Resources;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Analysis.Persistence;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController : ControllerBase
{
    private readonly IPersistenceService _persistenceService;
    private readonly IUserContext _userContext;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(
        IPersistenceService persistenceService,
        IUserContext userContext,
        ILogger<ResourcesController> logger)
    {
        _persistenceService = persistenceService;
        _userContext = userContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? courseId,
        CancellationToken cancellationToken)
    {
        try
        {
            var entities = await _persistenceService.ListNetworkResourcesAsync(
                new NetworkResourcesListQuery(courseId, true),
                cancellationToken);
            var items = entities.Select(MapToDto).ToList();

            return Ok(items);
        }
        catch (Exception ex) when (ApiExceptionClassifier.IsDatabaseFailure(ex))
        {
            _logger.LogWarning(ex, "Resources endpoint degraded due to database/schema issue.");
            return this.DegradedOk(Array.Empty<NetworkResourceListItemDto>());
        }
    }

    [HttpPost]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult<NetworkResourceListItemDto>> Create(
        [FromBody] NetworkResourceCreateDto dto,
        CancellationToken cancellationToken)
    {
        if (dto.CourseId <= 0)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "RESOURCE_COURSE_REQUIRED", "courseId is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Category))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "RESOURCE_CATEGORY_REQUIRED", "category is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "RESOURCE_TITLE_REQUIRED", "title is required.");
        }

        var courseExists = await _persistenceService.CourseExistsAsync(
            new CourseByIdQuery(dto.CourseId),
            cancellationToken);
        if (!courseExists)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "RESOURCE_COURSE_NOT_FOUND", "Course not found.");
        }

        var now = DateTime.UtcNow;
        var entity = new NetworkResource
        {
            CourseId = dto.CourseId,
            Category = dto.Category.Trim(),
            Title = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            Link = dto.Link?.Trim(),
            SortOrder = dto.SortOrder,
            IsEnabled = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _persistenceService.CreateNetworkResourceAsync(
            new CreateNetworkResourceCommand(entity),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NetworkResourceListItemDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await _persistenceService.GetNetworkResourceByIdAsync(
            new NetworkResourceByIdQuery(id),
            cancellationToken);

        if (entity == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }

        if (!entity.IsEnabled && !await CanManageResourcesAsync(cancellationToken))
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }

        return Ok(MapToDto(entity));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult<NetworkResourceListItemDto>> Update(
        int id,
        [FromBody] NetworkResourceUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var entity = await _persistenceService.UpdateNetworkResourceAsync(
            new UpdateNetworkResourceCommand(
                id,
                dto.Category,
                dto.Title,
                dto.Description,
                dto.Link,
                dto.SortOrder,
                dto.IsEnabled,
                DateTime.UtcNow),
            cancellationToken);

        if (entity == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }
        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var deleted = await _persistenceService.DeleteNetworkResourceAsync(
            new DeleteNetworkResourceCommand(id),
            cancellationToken);
        if (!deleted)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }

        return NoContent();
    }

    private async Task<bool> CanManageResourcesAsync(CancellationToken cancellationToken)
    {
        var user = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (user == null)
        {
            return false;
        }

        return string.Equals(user.Role, AppUserRole.Teacher, StringComparison.OrdinalIgnoreCase)
               || string.Equals(user.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase);
    }

    private static NetworkResourceListItemDto MapToDto(NetworkResource entity)
    {
        return new NetworkResourceListItemDto
        {
            Id = entity.Id,
            CourseId = entity.CourseId,
            Category = entity.Category,
            Title = entity.Title,
            Description = entity.Description,
            Link = entity.Link,
            SortOrder = entity.SortOrder,
            IsEnabled = entity.IsEnabled,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
