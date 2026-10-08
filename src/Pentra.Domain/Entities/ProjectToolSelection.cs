using Pentra.Domain.Common;

namespace Pentra.Domain.Entities;

/// <summary>
/// Associates a <see cref="SecurityTool"/> with a <see cref="Project"/> for a
/// specific <see cref="PentestPhase"/>. Represents intent to use a tool — not execution.
/// </summary>
public class ProjectToolSelection : AuditableEntity
{
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    public int PhaseId { get; set; }

    public PentestPhase? Phase { get; set; }

    public int SecurityToolId { get; set; }

    public SecurityTool? SecurityTool { get; set; }

    public string Notes { get; set; } = string.Empty;
}
