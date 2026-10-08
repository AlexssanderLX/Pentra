using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// An entry in the security tooling catalog (e.g. Nmap, Nuclei).
/// Catalog data only — this checkpoint never executes these tools.
/// </summary>
public class SecurityTool : AuditableEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable key used for seeding and lookups, e.g. "nmap".</summary>
    public string Slug { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public ToolCategory Category { get; set; }

    /// <summary>Canonical executable/binary name, kept for future adapters.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Reference/documentation URL.</summary>
    public string ReferenceUrl { get; set; } = string.Empty;

    /// <summary>Phase this tool is most commonly used in (suggested default).</summary>
    public int DefaultPhaseId { get; set; }

    public PentestPhase? DefaultPhase { get; set; }

    public ICollection<ProjectToolSelection> Selections { get; set; } = new List<ProjectToolSelection>();
}
