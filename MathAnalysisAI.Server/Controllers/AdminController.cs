using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Admin;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/admin")]
[RequireAuth]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly ApplicationDbContext _db;
    private readonly AuthOptions _authOptions;

    public AdminController(AdminService adminService, ApplicationDbContext db, IOptions<AuthOptions> authOptions)
    {
        _adminService = adminService;
        _db = db;
        _authOptions = authOptions.Value ?? new AuthOptions();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();
        var dashboard = await _adminService.GetDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("users")]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!IsAdmin()) return Forbid();

        var users = await _adminService.ListUsersAsync(search, role, page, pageSize, cancellationToken);
        var total = await _adminService.GetUserCountAsync(search, role, cancellationToken);

        return Ok(new { items = users, totalCount = total, page, pageSize });
    }

    [HttpPut("users/{userId:int}/role")]
    public async Task<IActionResult> UpdateUserRole(
        int userId,
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsAdmin()) return Forbid();

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var success = await _adminService.UpdateUserRoleAsync(userId, request.Role, cancellationToken);
        if (!success)
            return NotFound(new { message = "User not found or invalid role." });

        return Ok(new { message = "Role updated successfully." });
    }

    [HttpPost("teachers")]
    public async Task<ActionResult> CreateTeacher(
        [FromBody] CreateTeacherRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsAdminOrTeacher()) return Forbid();

        var (success, message, userId) = await _adminService.CreateTeacherAsync(
            request.Username.Trim(),
            request.Password,
            request.RealName,
            _authOptions.BcryptWorkFactor,
            cancellationToken);

        if (!success)
            return BadRequest(new { message });

        return Ok(new { userId, username = request.Username.Trim(), message });
    }

    [HttpPost("import-students")]
    public async Task<ActionResult> ImportStudents(
        [FromBody] ImportStudentsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!IsAdminOrTeacher()) return Forbid();

        if (request.Students == null || request.Students.Count == 0)
            return BadRequest(new { message = "No students to import." });

        var (created, skipped, errors) = await _adminService.ImportStudentsAsync(
            request.TeacherId,
            request.Students,
            _authOptions.BcryptWorkFactor,
            cancellationToken);

        return Ok(new { created, skipped, errors, total = request.Students.Count });
    }

    [HttpGet("teachers")]
    public async Task<ActionResult> ListTeachers(CancellationToken cancellationToken)
    {
        if (!IsAdminOrTeacher()) return Forbid();

        var teachers = await _db.AppUsers
            .AsNoTracking()
            .Where(x => x.Role == AppUserRole.Teacher || x.Role == AppUserRole.Admin)
            .OrderBy(x => x.Role == AppUserRole.Admin ? 0 : 1)
            .ThenBy(x => x.RealName)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.RealName,
                x.Role,
                StudentCount = x.Students.Count
            })
            .ToListAsync(cancellationToken);

        return Ok(teachers);
    }

    [HttpGet("teachers/{teacherId:int}/students")]
    public async Task<ActionResult> ListTeacherStudents(
        int teacherId,
        CancellationToken cancellationToken)
    {
        if (!IsAdminOrTeacher()) return Forbid();

        var students = await _db.AppUsers
            .AsNoTracking()
            .Where(x => x.TeacherId == teacherId)
            .OrderBy(x => x.StudentNumber ?? x.RealName ?? x.Username)
            .Select(x => new
            {
                x.Id,
                x.Username,
                x.RealName,
                x.StudentNumber,
                x.ClassName
            })
            .ToListAsync(cancellationToken);

        return Ok(students);
    }

    private bool IsAdmin()
    {
        var currentUser = HttpContext.GetCurrentUser();
        return currentUser != null
            && string.Equals(currentUser.Role, "admin", StringComparison.OrdinalIgnoreCase);
    }

    private bool IsAdminOrTeacher()
    {
        var currentUser = HttpContext.GetCurrentUser();
        return currentUser != null
            && (string.Equals(currentUser.Role, "admin", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentUser.Role, "teacher", StringComparison.OrdinalIgnoreCase));
    }
}
