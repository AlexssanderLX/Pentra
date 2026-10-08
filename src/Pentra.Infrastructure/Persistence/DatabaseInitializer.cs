using System.Runtime.InteropServices;
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
    /// <param name="shareFilePermissions">
    /// When true (the privileged runner, which initializes first), relax the SQLite
    /// file permissions to 0666 so the non-root web container can share the file.
    /// </param>
    public static async Task InitializeAsync(IServiceProvider services, bool shareFilePermissions = false, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PentraDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<ISystemClock>();

        var connectionString = db.Database.GetConnectionString();
        EnsureDatabaseDirectory(connectionString);

        await db.Database.MigrateAsync(ct);

        // WAL lets the web app and the runner process share the SQLite file with
        // concurrent readers and a single writer. The setting persists in the file.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;", ct);

        await DataSeeder.SeedAsync(db, clock, ct);

        if (shareFilePermissions)
        {
            RelaxDatabaseFilePermissions(connectionString);
        }
    }

    /// <summary>
    /// Runs <see cref="InitializeAsync"/>, retrying on transient "readonly/locked"
    /// errors. The web container may start before the runner has created and
    /// relaxed the shared database file; this waits that out instead of crashing.
    /// </summary>
    public static async Task InitializeWithRetryAsync(IServiceProvider services, int maxAttempts = 30, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await InitializeAsync(services, shareFilePermissions: false, ct);
                return;
            }
            catch (SqliteException ex) when (attempt < maxAttempts && IsTransient(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }
    }

    private static bool IsTransient(SqliteException ex) =>
        ex.SqliteErrorCode is 8 /* readonly */ or 5 /* busy */ or 6 /* locked */;

    private static void RelaxDatabaseFilePermissions(string? connectionString)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) || string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrWhiteSpace(dataSource))
        {
            return;
        }

        var full = Path.GetFullPath(dataSource);
        const UnixFileMode rw = UnixFileMode.UserRead | UnixFileMode.UserWrite |
                                UnixFileMode.GroupRead | UnixFileMode.GroupWrite |
                                UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

        foreach (var path in new[] { full, full + "-wal", full + "-shm" })
        {
            try
            {
                if (File.Exists(path))
                {
                    File.SetUnixFileMode(path, rw);
                }
            }
            catch (Exception)
            {
                // best-effort; the web retry loop covers the gap
            }
        }
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
