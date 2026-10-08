using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pentra.Domain.Abstractions;
using Pentra.Infrastructure.Persistence;

namespace Pentra.Tests.TestSupport;

/// <summary>A clock returning a fixed, controllable instant for deterministic tests.</summary>
public sealed class FakeClock : ISystemClock
{
    public DateTime UtcNow { get; set; } = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Owns an open in-memory SQLite connection and a context built against it,
/// so the schema (created via the real migrations' model) persists for the test.
/// </summary>
public sealed class TestHarness : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<PentraDbContext>()
            .UseSqlite(_connection)
            .Options;

        Db = new PentraDbContext(options);
        Db.Database.EnsureCreated();
    }

    public PentraDbContext Db { get; }

    public FakeClock Clock { get; } = new();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
