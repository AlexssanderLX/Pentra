using Pentra.Domain.Entities;

namespace Pentra.Domain.Abstractions;

// ---------------------------------------------------------------------------
// Contracts for FUTURE tool execution. NOTHING in Checkpoint 1 implements these
// in a way that runs a scanner. They exist so later checkpoints can plug in
// Tool Adapters and Docker Runners behind an authorization gate, without
// reshaping the domain. Keeping them here documents the intended boundary.
// ---------------------------------------------------------------------------

/// <summary>
/// A fully-resolved, validated request to run a single tool against an
/// authorized target. Construction of this object is expected to be the only
/// path to execution in future checkpoints.
/// </summary>
public sealed record ToolExecutionRequest(
    int ProjectId,
    int PhaseId,
    SecurityTool Tool,
    ScopeTarget Target,
    IReadOnlyList<string> Arguments);

/// <summary>Outcome of a (future) tool run.</summary>
public sealed record ToolExecutionResult(
    bool Succeeded,
    int ExitCode,
    string Output,
    string Error,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

/// <summary>
/// Authorization gate for execution. An implementation must confirm the target
/// is in-scope and explicitly authorized, and that the project is in a state
/// that permits running tools. Checkpoint 1 ships a policy that always denies.
/// </summary>
public interface IToolExecutionPolicy
{
    /// <summary>
    /// Returns an authorization decision for the given request.
    /// </summary>
    ToolExecutionAuthorization Authorize(ToolExecutionRequest request);
}

/// <summary>Result of an authorization check.</summary>
public sealed record ToolExecutionAuthorization(bool IsAllowed, string Reason)
{
    public static ToolExecutionAuthorization Allow() => new(true, "Authorized.");

    public static ToolExecutionAuthorization Deny(string reason) => new(false, reason);
}

/// <summary>
/// Adapter that knows how to translate a <see cref="ToolExecutionRequest"/> into
/// a concrete command line for a specific tool. Implemented per tool in a
/// future checkpoint. Building a command line is pure and side-effect free.
/// </summary>
public interface IToolAdapter
{
    /// <summary>Slug of the tool this adapter handles (matches <see cref="SecurityTool.Slug"/>).</summary>
    string ToolSlug { get; }

    /// <summary>Builds (but does not run) the argument vector for the request.</summary>
    IReadOnlyList<string> BuildCommand(ToolExecutionRequest request);
}

/// <summary>
/// Executes a prepared command in an isolated environment (e.g. an ephemeral
/// Docker container). Checkpoint 1 provides no runtime implementation and the
/// Docker socket is never exposed to the web application.
/// </summary>
public interface IToolRunner
{
    /// <summary>Runs the request after policy authorization. Future checkpoint.</summary>
    Task<ToolExecutionResult> RunAsync(ToolExecutionRequest request, CancellationToken cancellationToken = default);
}
