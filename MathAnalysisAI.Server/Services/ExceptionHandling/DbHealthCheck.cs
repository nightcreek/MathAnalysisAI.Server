using MathAnalysisAI.Server.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MathAnalysisAI.Server.Services.ExceptionHandling;

public class DbHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _db;

    public DbHealthCheck(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Database connection is healthy.")
                : HealthCheckResult.Unhealthy("Database connection failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed.", ex);
        }
    }
}
