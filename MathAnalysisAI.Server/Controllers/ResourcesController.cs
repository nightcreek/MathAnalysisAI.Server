using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Resources;
using MathAnalysisAI.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MathAnalysisAI.Server.Controllers
{
    [ApiController]
    [Route("api/resources")]
    public class ResourcesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _environment;

        public ResourcesController(
            ApplicationDbContext db,
            IConfiguration configuration,
            IWebHostEnvironment environment)
        {
            _db = db;
            _configuration = configuration;
            _environment = environment;
        }

        [HttpGet]
        public async Task<ActionResult<List<NetworkResourceListItemDto>>> List(
            [FromQuery] int? courseId,
            CancellationToken cancellationToken)
        {
            var query = _db.NetworkResources.AsNoTracking();

            if (courseId.HasValue && courseId.Value > 0)
            {
                query = query.Where(x => x.CourseId == courseId.Value && x.IsEnabled);
            }
            else
            {
                query = query.Where(x => x.IsEnabled);
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

        [HttpPost]
        public async Task<ActionResult<NetworkResourceListItemDto>> Create(
            [FromBody] NetworkResourceCreateDto dto,
            CancellationToken cancellationToken)
        {
            var authResult = EnsureResourceManager();
            if (authResult != null) return authResult;

            if (dto.CourseId <= 0)
                return BadRequest(new { message = "courseId is required." });

            if (string.IsNullOrWhiteSpace(dto.Category))
                return BadRequest(new { message = "category is required." });

            if (string.IsNullOrWhiteSpace(dto.Title))
                return BadRequest(new { message = "title is required." });

            var courseExists = await _db.Courses.AnyAsync(x => x.Id == dto.CourseId, cancellationToken);
            if (!courseExists)
                return BadRequest(new { message = "Course not found." });

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
                return NotFound();

            return Ok(MapToDto(entity));
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult<NetworkResourceListItemDto>> Update(
            int id,
            [FromBody] NetworkResourceUpdateDto dto,
            CancellationToken cancellationToken)
        {
            var authResult = EnsureResourceManager();
            if (authResult != null) return authResult;

            var entity = await _db.NetworkResources.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null)
                return NotFound();

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
        public async Task<ActionResult> Delete(
            int id,
            CancellationToken cancellationToken)
        {
            var authResult = EnsureResourceManager();
            if (authResult != null) return authResult;

            var entity = await _db.NetworkResources.FindAsync(new object[] { id }, cancellationToken);
            if (entity == null)
                return NotFound();

            _db.NetworkResources.Remove(entity);
            await _db.SaveChangesAsync(cancellationToken);

            return NoContent();
        }

        private ActionResult? EnsureResourceManager()
        {
            if (ShouldAllowDevelopmentOverride())
                return null;

            var user = (AppUser?)HttpContext.Items["CurrentUser"];
            if (user == null)
                return Unauthorized(new { message = "Not logged in." });

            var role = user.Role?.Trim();
            if (string.Equals(role, "teacher", StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Forbidden." });
        }

        private bool ShouldAllowDevelopmentOverride()
        {
            var enabled = _configuration.GetValue<bool>("Auth:EnableDevelopmentMaterialAccessOverride");
            return enabled && _environment.IsDevelopment();
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
}
