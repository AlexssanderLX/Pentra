using Pentra.Domain.Common;

namespace Pentra.Domain.Entities;

/// <summary>
/// Initial structure for engagement reports. Checkpoint 1 stores a titled draft
/// with a summary; rendering/export is a future checkpoint.
/// </summary>
public class ReportDraft : AuditableEntity
{
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public bool IsFinalized { get; set; }
}
