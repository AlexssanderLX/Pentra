using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// One execution of a tool against a single authorized target, within a project
/// and phase. Created by the web app (status <see cref="ExecutionStatus.Pending"/>)
/// after authorization, then claimed and driven by the privileged runner.
/// Also serves as the automatic evidence record for the run.
/// </summary>
public class ToolRun : AuditableEntity
{
    // --- What is being run ---------------------------------------------------
    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    public int PhaseId { get; set; }
    public PentestPhase? Phase { get; set; }

    public int SecurityToolId { get; set; }
    public SecurityTool? SecurityTool { get; set; }

    /// <summary>
    /// The authorized target this run was created against. Nullable because a run
    /// is retained as evidence even if the target is later removed from scope;
    /// <see cref="TargetValue"/> is the immutable snapshot the runner uses.
    /// </summary>
    public int? ScopeTargetId { get; set; }
    public ScopeTarget? ScopeTarget { get; set; }

    /// <summary>Slug of the tool/adapter (e.g. "nmap"). Denormalized for the runner.</summary>
    public string ToolSlug { get; set; } = string.Empty;

    /// <summary>Pinned, approved container image used for this run (image:tag).</summary>
    public string ImageRef { get; set; } = string.Empty;

    /// <summary>Resolved target value at request time (immutable snapshot).</summary>
    public string TargetValue { get; set; } = string.Empty;

    // --- Approved inputs (never arbitrary) -----------------------------------
    /// <summary>The validated high-level parameter set, serialized as JSON.</summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>The exact argument vector approved for the tool, serialized as JSON.</summary>
    public string ApprovedArgumentsJson { get; set; } = "[]";

    /// <summary>True when the tool performs active scanning (requires confirmation).</summary>
    public bool IsActiveScan { get; set; }

    /// <summary>Operator's explicit confirmation to run an active scan.</summary>
    public bool ConfirmedActive { get; set; }

    // --- Authorization decision (recorded) -----------------------------------
    public bool AuthorizationAllowed { get; set; }
    public string AuthorizationReason { get; set; } = string.Empty;

    // --- State ---------------------------------------------------------------
    public ExecutionStatus Status { get; set; } = ExecutionStatus.Pending;

    /// <summary>Set by the web app to request cooperative cancellation.</summary>
    public bool CancelRequested { get; set; }

    /// <summary>Hard execution time limit in seconds (tool-specific).</summary>
    public int TimeoutSeconds { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }

    public int? ExitCode { get; set; }
    public string FailureReason { get; set; } = string.Empty;

    // --- Results / evidence --------------------------------------------------
    /// <summary>Sanitized, truncated raw tool output (stdout).</summary>
    public string RawOutput { get; set; } = string.Empty;

    /// <summary>Truncated error output (stderr).</summary>
    public string ErrorOutput { get; set; } = string.Empty;

    /// <summary>Structured, parser-produced result (JSON). Never auto-classified as a vuln.</summary>
    public string ResultJson { get; set; } = string.Empty;

    /// <summary>SHA-256 of the raw output artifact, for integrity.</summary>
    public string ArtifactSha256 { get; set; } = string.Empty;

    // --- Concurrency / claiming ---------------------------------------------
    /// <summary>When the runner claimed this run (Pending → Running).</summary>
    public DateTime? ClaimedAt { get; set; }

    /// <summary>Identifier of the runner instance that claimed the run.</summary>
    public string RunnerId { get; set; } = string.Empty;

    public ICollection<ToolRunLogLine> Logs { get; set; } = new List<ToolRunLogLine>();

    /// <summary>A run is active (occupies a concurrency slot / blocks duplicates).</summary>
    public bool IsActive => Status is ExecutionStatus.Pending or ExecutionStatus.Running;
}
