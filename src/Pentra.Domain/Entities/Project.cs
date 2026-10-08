using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// A penetration-testing engagement. Aggregate root for scope, tool selection,
/// evidence and reporting.
/// </summary>
public class Project : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    public string Client { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    /// <summary>
    /// Free-text rules of engagement / authorization reference.
    /// Scope authorization is a precondition for any future execution.
    /// </summary>
    public string EngagementNotes { get; set; } = string.Empty;

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public ICollection<ScopeTarget> Targets { get; set; } = new List<ScopeTarget>();

    public ICollection<ProjectToolSelection> ToolSelections { get; set; } = new List<ProjectToolSelection>();

    public ICollection<Evidence> Evidence { get; set; } = new List<Evidence>();

    public ICollection<ReportDraft> Reports { get; set; } = new List<ReportDraft>();

    public ICollection<ChangeHistoryEntry> History { get; set; } = new List<ChangeHistoryEntry>();

    public ICollection<ToolRun> Runs { get; set; } = new List<ToolRun>();
}
