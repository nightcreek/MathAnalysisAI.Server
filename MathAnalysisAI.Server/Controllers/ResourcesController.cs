using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Resources;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/resources")]
public class ResourcesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IUserContext _userContext;
    private readonly ILogger<ResourcesController> _logger;

    public ResourcesController(
        ApplicationDbContext db,
        IUserContext userContext,
        ILogger<ResourcesController> logger)
    {
        _db = db;
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
            var query = _db.NetworkResources.AsNoTracking().Where(x => x.IsEnabled);

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(x => x.CourseId == courseId.Value);
            }

            var items = await query
                .OrderBy(x => x.Category)
                .ThenBy(x => x.SortOrder)
                .ThenBy(x => x.Title)
                .Select(x => new NetworkResourceListItemDto
                {
                    Id = x.Id,
                    CourseId = x.CourseId,
                    Category = x.Category,
                    Title = x.Title,
                    Description = x.Description,
                    Link = x.Link,
                    SortOrder = x.SortOrder,
                    IsEnabled = x.IsEnabled,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync(cancellationToken);

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

        var courseExists = await _db.Courses.AnyAsync(x => x.Id == dto.CourseId, cancellationToken);
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

        _db.NetworkResources.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, MapToDto(entity));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<NetworkResourceListItemDto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.NetworkResources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

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
        var entity = await _db.NetworkResources.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }

        if (dto.Category != null) entity.Category = dto.Category.Trim();
        if (dto.Title != null) entity.Title = dto.Title.Trim();
        if (dto.Description != null) entity.Description = dto.Description.Trim();
        if (dto.Link != null) entity.Link = dto.Link.Trim();
        if (dto.SortOrder.HasValue) entity.SortOrder = dto.SortOrder.Value;
        if (dto.IsEnabled.HasValue) entity.IsEnabled = dto.IsEnabled.Value;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);
        return Ok(MapToDto(entity));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult> Delete(
        int id,
        CancellationToken cancellationToken)
    {
        var entity = await _db.NetworkResources.FindAsync(new object[] { id }, cancellationToken);
        if (entity == null)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND", "Resource not found.");
        }

        _db.NetworkResources.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);

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
