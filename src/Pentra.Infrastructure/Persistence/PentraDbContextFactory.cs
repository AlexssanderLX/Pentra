using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Pentra.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so EF Core tooling (migrations) can construct the context
/// without the web host. Not used at runtime.
/// </summary>
public sealed class PentraDbContextFactory : IDesignTimeDbContextFactory<PentraDbContext>
{
    public PentraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<PentraDbContext>()
            .UseSqlite("Data Source=pentra-design.db")
            .Options;

        return new PentraDbContext(options);
    }
}
