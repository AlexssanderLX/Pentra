using Pentra.Domain.Common;

namespace Pentra.Domain.Entities;

/// <summary>
/// Initial structure for engagement evidence. Checkpoint 1 stores metadata only;
/// artifact capture/attachment is a future checkpoint.
/// </summary>
public class Evidence : AuditableEntity
{
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>Optional phase this evidence belongs to.</summary>
    public int? PhaseId { get; set; }

    public PentestPhase? Phase { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
