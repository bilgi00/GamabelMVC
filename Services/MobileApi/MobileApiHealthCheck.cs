using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using gamabelmvc.Data;

namespace gamabelmvc.Services.MobileApi;

public sealed class MobileApiHealthCheck(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<MobileApiHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!MobileJwtSettings.HasValidSigningKey(configuration))
        {
            logger.LogError("Mobile API readiness failed: JWT signing key is missing or shorter than 32 UTF-8 bytes.");
            return HealthCheckResult.Unhealthy();
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<StokSayimDbContext>();
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            logger.LogError("Mobile API readiness failed: the configured database is not reachable.");
            return HealthCheckResult.Unhealthy();
        }

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync(cancellationToken);
        var migrationNames = pendingMigrations.ToArray();
        if (migrationNames.Length > 0)
        {
            logger.LogError(
                "Mobile API readiness failed: {PendingMigrationCount} EF Core migration(s) are pending: {MigrationNames}.",
                migrationNames.Length,
                string.Join(", ", migrationNames));
            return HealthCheckResult.Unhealthy();
        }

        return HealthCheckResult.Healthy();
    }
}
