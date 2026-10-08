using Microsoft.EntityFrameworkCore;
using Pentra.Domain.Entities;

namespace Pentra.Application.Abstractions;

/// <summary>
/// Persistence contract consumed by the application services. Implemented by the
/// EF Core <c>PentraDbContext</c> in the Infrastructure layer. Keeping the
/// surface narrow lets services stay independent of the concrete context.
/// </summary>
public interface IPentraDbContext
{
    DbSet<Project> Projects { get; }
    DbSet<ScopeTarget> Targets { get; }
    DbSet<PentestPhase> Phases { get; }
    DbSet<SecurityTool> Tools { get; }
    DbSet<ProjectToolSelection> ToolSelections { get; }
    DbSet<Evidence> Evidence { get; }
    DbSet<ReportDraft> Reports { get; }
    DbSet<ChangeHistoryEntry> History { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
