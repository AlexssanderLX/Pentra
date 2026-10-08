using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// A single captured log line for a <see cref="ToolRun"/>. Append-only; the
/// dashboard streams new lines by querying for <see cref="Seq"/> greater than the
/// last one it has seen.
/// </summary>
public class ToolRunLogLine : AuditableEntity
{
    public int ToolRunId { get; set; }
    public ToolRun? ToolRun { get; set; }

    /// <summary>Monotonic sequence within a run, starting at 1.</summary>
    public int Seq { get; set; }

    public LogStream Stream { get; set; }

    public string Text { get; set; } = string.Empty;
}
