using MathAnalysisAI.Server.Data;
using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Models;
using MathAnalysisAI.Server.Options;
using MathAnalysisAI.Server.Services.Admin;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthPolicies.AuthenticatedUser)]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;
    private readonly ApplicationDbContext _db;
    private readonly AuthOptions _authOptions;
    private readonly IUserContext _userContext;

    public AdminController(
        AdminService adminService,
        ApplicationDbContext db,
        IOptions<AuthOptions> authOptions,
        IUserContext userContext)
    {
        _adminService = adminService;
        _db = db;
        _authOptions = authOptions.Value ?? new AuthOptions();
        _userContext = userContext;
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var dashboard = await _adminService.GetDashboardAsync(cancellationToken);
        return Ok(dashboard);
    }

    [HttpGet("users")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> ListUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var users = await _adminService.ListUsersAsync(search, role, page, pageSize, cancellationToken);
        var total = await _adminService.GetUserCountAsync(search, role, cancellationToken);

        return Ok(new { items = users, totalCount = total, page, pageSize });
    }

    [HttpPut("users/{userId:int}/role")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<IActionResult> UpdateUserRole(
        int userId,
        [FromBody] UpdateUserRoleRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var success = await _adminService.UpdateUserRoleAsync(userId, request.Role, cancellationToken);
        if (!success)
        {
            return this.ApiError(StatusCodes.Status404NotFound, "ADMIN_USER_NOT_FOUND_OR_ROLE_INVALID", "User not found or invalid role.");
        }

        return Ok(new { message = "Role updated successfully." });
    }

    [HttpPost("teachers")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult> CreateTeacher(
        [FromBody] CreateTeacherRequestDto request,
        CancellationToken cancellationToken)
    {
        var (success, message, userId) = await _adminService.CreateTeacherAsync(
            request.Username.Trim(),
            request.Password,
            request.RealName,
            _authOptions.BcryptWorkFactor,
            cancellationToken);

        if (!success)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "ADMIN_CREATE_TEACHER_FAILED", message);
        }

        return Ok(new { userId, username = request.Username.Trim(), message });
    }

    [HttpPost("import-students")]
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult> ImportStudents(
        [FromBody] ImportStudentsRequestDto request,
        CancellationToken cancellationToken)
    {
        if (request.Students == null || request.Students.Count == 0)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "ADMIN_NO_STUDENTS", "No students to import.");
        }

        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        var isAdmin = string.Equals(currentUser.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase);
        var effectiveTeacherId = request.TeacherId;

        if (isAdmin)
        {
            if (effectiveTeacherId <= 0)
            {
                return this.ApiError(StatusCodes.Status400BadRequest, "ADMIN_TEACHER_REQUIRED", "TeacherId is required.");
            }
        }
        else
        {
            if (request.TeacherId > 0 && request.TeacherId != currentUser.Id)
            {
                return this.ApiError(StatusCodes.Status403Forbidden, "ADMIN_TEACHER_SCOPE_FORBIDDEN", "Teachers can only import students into their own class.");
            }

            effectiveTeacherId = currentUser.Id;
        }

        var (created, skipped, errors) = await _adminService.ImportStudentsAsync(
            effectiveTeacherId,
            request.Students,
            _authOptions.BcryptWorkFactor,
            cancellationToken);

        return Ok(new { created, skipped, errors, total = request.Students.Count, teacherId = effectiveTeacherId });
    }

    [HttpGet("teachers")]
    [Authorize(Policy = AuthPolicies.AdminOnly)]
    public async Task<ActionResult> ListTeachers(CancellationToken cancellationToken)
    {
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
    [Authorize(Policy = AuthPolicies.TeacherOrAdmin)]
    public async Task<ActionResult> ListTeacherStudents(
        int teacherId,
        CancellationToken cancellationToken)
    {
        var currentUser = await _userContext.GetCurrentUserAsync(cancellationToken);
        if (currentUser == null)
        {
            return this.ApiError(StatusCodes.Status401Unauthorized, "AUTH_NOT_LOGGED_IN", "Not logged in.");
        }

        var isAdmin = string.Equals(currentUser.Role, AppUserRole.Admin, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && teacherId != currentUser.Id)
        {
            return this.ApiError(StatusCodes.Status403Forbidden, "ADMIN_TEACHER_SCOPE_FORBIDDEN", "Teachers can only view their own students.");
        }

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
}
