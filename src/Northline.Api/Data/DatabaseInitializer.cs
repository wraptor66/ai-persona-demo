using Microsoft.EntityFrameworkCore;

namespace Northline.Api.Data;

/// <summary>Creates and seeds the database on startup, waiting for SQL Server to come up.</summary>
public static class DatabaseInitializer
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(3);

    /// <summary>Ensures the schema exists and seed data is present.</summary>
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<NorthlineDbContext>();
                await db.Database.EnsureCreatedAsync(ct);
                logger.LogInformation("Database ready (attempt {Attempt}).", attempt);
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning("Database not ready (attempt {Attempt}/{Max}): {Message}", attempt, MaxAttempts, ex.Message);
                await Task.Delay(RetryDelay, ct);
            }
        }
    }
}
