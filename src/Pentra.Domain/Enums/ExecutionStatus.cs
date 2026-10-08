namespace Pentra.Domain.Enums;

/// <summary>
/// Lifecycle state of a single tool execution (a <c>ToolRun</c>).
/// Transitions are enforced by the execution engine.
/// </summary>
public enum ExecutionStatus
{
    /// <summary>Authorized and queued, not yet claimed by the runner.</summary>
    Pending = 0,

    /// <summary>Claimed by the runner; the tool container is running.</summary>
    Running = 1,

    /// <summary>Finished with a result (tool exited, regardless of findings).</summary>
    Completed = 2,

    /// <summary>The tool or runner failed (non-zero exit, Docker error, interrupted).</summary>
    Failed = 3,

    /// <summary>Cancelled on request before natural completion.</summary>
    Cancelled = 4,

    /// <summary>Exceeded its execution time limit and was stopped.</summary>
    TimedOut = 5
}
