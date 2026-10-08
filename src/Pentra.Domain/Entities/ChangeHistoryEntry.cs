using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// An append-only record of a change to a project or one of its children.
/// Provides the "basic change/state history" required by the checkpoint.
/// </summary>
public class ChangeHistoryEntry : AuditableEntity
{
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public ChangeAction Action { get; set; }

    /// <summary>The entity type affected, e.g. "Project", "ScopeTarget".</summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>Human-readable summary of what changed.</summary>
    public string Summary { get; set; } = string.Empty;
}
