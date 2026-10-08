using Pentra.Application.Common;
using Pentra.Application.Models;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Abstractions;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListItem>> GetListAsync(CancellationToken ct = default);

    /// <summary>Loads a project with targets, tool selections and history.</summary>
    Task<Project?> GetDetailAsync(int id, CancellationToken ct = default);

    Task<Result<int>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default);

    Task<Result> UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default);

    Task<Result> DeleteAsync(int id, CancellationToken ct = default);
}

public interface ITargetService
{
    Task<Result<int>> AddAsync(CreateTargetRequest request, CancellationToken ct = default);

    Task<Result> RemoveAsync(int targetId, CancellationToken ct = default);
}

public interface IToolCatalogService
{
    Task<IReadOnlyList<SecurityTool>> GetCatalogAsync(CancellationToken ct = default);

    /// <summary>Toggles a tool selection for a project within a phase.</summary>
    Task<Result> ToggleSelectionAsync(int projectId, int phaseId, int toolId, CancellationToken ct = default);
}

public interface IPhaseService
{
    Task<IReadOnlyList<PentestPhase>> GetPhasesAsync(CancellationToken ct = default);
}

public interface IChangeHistoryService
{
    Task RecordAsync(int projectId, ChangeAction action, string entityType, string summary, CancellationToken ct = default);

    Task<IReadOnlyList<ChangeHistoryEntry>> GetForProjectAsync(int projectId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<DashboardSummary> GetSummaryAsync(CancellationToken ct = default);
}
