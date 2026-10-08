using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pentra.Domain.Abstractions;

namespace Pentra.Infrastructure.Persistence;

/// <summary>
/// Applies pending migrations and seeds reference data at application startup.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PentraDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

        EnsureDatabaseDirectory(db.Database.GetConnectionString());

        await db.Database.MigrateAsync(ct);
        await DataSeeder.SeedAsync(db, clock, ct);
    }

    /// <summary>
    /// SQLite does not create intermediate directories for the database file,
    /// so create the containing folder (e.g. a mounted volume path) if needed.
    /// </summary>
    private static void EnsureDatabaseDirectory(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
