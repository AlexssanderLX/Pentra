using Pentra.Domain.Entities;

namespace Pentra.Web.Models;

/// <summary>Everything the project detail page needs to render in one object.</summary>
public sealed class ProjectDetailsViewModel
{
    public required Project Project { get; init; }

    public required IReadOnlyList<PentestPhase> Phases { get; init; }

    public required IReadOnlyList<SecurityTool> Catalog { get; init; }

    public required IReadOnlyList<ChangeHistoryEntry> History { get; init; }

    /// <summary>Set of (PhaseId, ToolId) pairs currently selected for this project.</summary>
    public required HashSet<(int PhaseId, int ToolId)> SelectedToolKeys { get; init; }

    /// <summary>Form model used by the inline "add target" form.</summary>
    public TargetFormViewModel NewTarget { get; init; } = new();

    /// <summary>Recent tool runs for this project (newest first).</summary>
    public IReadOnlyList<ToolRun> Runs { get; init; } = Array.Empty<ToolRun>();

    /// <summary>Catalog tools that have an execution adapter, with risk info.</summary>
    public IReadOnlyList<SecurityTool> ExecutableTools { get; init; } = Array.Empty<SecurityTool>();

    /// <summary>Slugs of executable tools that are active scans (need confirmation).</summary>
    public HashSet<string> ActiveToolSlugs { get; init; } = new();

    public IReadOnlyList<ScopeTarget> AuthorizedTargets =>
        Project.Targets.Where(t => t.IsAuthorized).OrderBy(t => t.Value).ToList();

    public bool IsSelected(int phaseId, int toolId) => SelectedToolKeys.Contains((phaseId, toolId));
}
