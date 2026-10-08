using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Application.Models;
using Pentra.Domain.Enums;

namespace Pentra.Application.Services;

public sealed class DashboardService : IDashboardService
{
    private const int RecentCount = 5;

    private readonly IPentraDbContext _db;

    public DashboardService(IPentraDbContext db) => _db = db;

    public async Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default)
    {
        var totalProjects = await _db.Projects.CountAsync(ct);
        var activeProjects = await _db.Projects.CountAsync(p => p.Status == ProjectStatus.Active, ct);
        var authorizedTargets = await _db.Targets.CountAsync(t => t.IsAuthorized, ct);
        var catalogTools = await _db.Tools.CountAsync(ct);

        var recent = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Take(RecentCount)
            .Select(p => new ProjectListItem(
                p.Id, p.Name, p.Client, p.Status, p.Targets.Count, p.ToolSelections.Count, p.CreatedAt))
            .ToListAsync(ct);

        var byStatus = await _db.Projects
            .GroupBy(p => p.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byStatusMap = byStatus.ToDictionary(x => x.Key, x => x.Count);

        return new DashboardSummary(
            totalProjects,
            activeProjects,
            authorizedTargets,
            catalogTools,
            recent,
            byStatusMap);
    }
}
