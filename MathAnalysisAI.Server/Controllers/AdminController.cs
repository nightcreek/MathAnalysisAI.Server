using MathAnalysisAI.Server.DTOs.Admin;
using MathAnalysisAI.Server.Filters;
using MathAnalysisAI.Server.Services.Admin;
using Microsoft.AspNetCore.Mvc;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/admin")]
[RequireAuth]
public class AdminController : ControllerBase
{
    private readonly AdminService _adminService;

    public AdminController(AdminService adminService)
    {
        _adminService = adminService;
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

    private bool IsAdmin()
    {
        var currentUser = HttpContext.GetCurrentUser();
        return currentUser != null
            && string.Equals(currentUser.Role, "admin", StringComparison.OrdinalIgnoreCase);
    }
}
