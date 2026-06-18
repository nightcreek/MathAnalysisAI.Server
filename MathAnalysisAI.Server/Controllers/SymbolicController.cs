using MathAnalysisAI.Server.DTOs.Symbolic;
using MathAnalysisAI.Server.Services.Auth;
using MathAnalysisAI.Server.Services.ExceptionHandling;
using MathAnalysisAI.Server.Services.Symbolic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace MathAnalysisAI.Server.Controllers;

[ApiController]
[Route("api/symbolic")]
[Authorize(Policy = AuthPolicies.AdminOnly)]
public class SymbolicController : ControllerBase
{
    private readonly ISymbolicMathService _symbolicMathService;

    public SymbolicController(ISymbolicMathService symbolicMathService)
    {
        _symbolicMathService = symbolicMathService;
    }

    [HttpPost("compute")]
    [EnableRateLimiting("symbolic")]
    public async Task<ActionResult<SymbolicComputeResponseDto>> Compute(
        [FromBody] SymbolicComputeRequestDto? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "SYMBOLIC_REQUEST_REQUIRED", "Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Operation))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "SYMBOLIC_OPERATION_REQUIRED", "Field 'operation' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Expression))
        {
            return this.ApiError(StatusCodes.Status400BadRequest, "SYMBOLIC_EXPRESSION_REQUIRED", "Field 'expression' is required.");
        }

        var response = await _symbolicMathService.ComputeAsync(request, cancellationToken);
        return Ok(response);
    }
}
