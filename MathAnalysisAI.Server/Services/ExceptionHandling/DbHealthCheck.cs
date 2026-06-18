using MathAnalysisAI.Server.Data;
using Microsoft.EntityFrameworkCore;
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
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("Database connection failed.");
            }

            var pendingMigrations = await _db.Database.GetPendingMigrationsAsync(cancellationToken);
            var pendingList = pendingMigrations.ToList();
            if (pendingList.Count > 0)
            {
                return HealthCheckResult.Unhealthy(
                    $"Database has pending migrations: {string.Join(", ", pendingList)}");
            }

            // Touch a few critical tables so readiness reflects schema availability, not just connectivity.
            await _db.Courses.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await _db.AppUsers.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);
            await _db.PromptProfiles.AsNoTracking().Select(x => x.Id).Take(1).ToListAsync(cancellationToken);

            return HealthCheckResult.Healthy("Database connection, migrations, and critical schema are ready.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database health check failed.", ex);
        }
    }
}
