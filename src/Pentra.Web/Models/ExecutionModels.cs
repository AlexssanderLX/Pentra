using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;

namespace Pentra.Web.Models;

/// <summary>Posted by the "start execution" form on the project page.</summary>
public sealed class StartRunForm
{
    public int ProjectId { get; set; }
    public int PhaseId { get; set; }
    public int SecurityToolId { get; set; }
    public int ScopeTargetId { get; set; }
    public bool ConfirmedActive { get; set; }

    // Nmap
    public bool NmapServiceDetection { get; set; }
    public int? NmapTopPorts { get; set; }
    public string? NmapTiming { get; set; }

    // httpx
    public bool HttpxFollowRedirects { get; set; }
    public bool HttpxTechDetect { get; set; } = true;

    // Subfinder
    public bool SubfinderAllSources { get; set; }
}

public sealed class RunDetailsViewModel
{
    public required ToolRun Run { get; init; }
    public required IReadOnlyList<ToolRunLogLine> Logs { get; init; }
    public ToolParseResult? Result { get; init; }
}
