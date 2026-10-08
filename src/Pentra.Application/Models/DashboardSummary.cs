using Pentra.Domain.Enums;

namespace Pentra.Application.Models;

/// <summary>Aggregated figures shown on the dashboard landing page.</summary>
public sealed record DashboardSummary(
    int TotalProjects,
    int ActiveProjects,
    int AuthorizedTargets,
    int CatalogTools,
    IReadOnlyList<ProjectListItem> RecentProjects,
    IReadOnlyDictionary<ProjectStatus, int> ProjectsByStatus);
