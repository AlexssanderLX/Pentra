using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Abstractions;

// ---------------------------------------------------------------------------
// Execution contracts. These define the boundary between:
//   * tool adapters  (pure: build argv + parse output for one tool),
//   * the execution policy (authorize a request),
//   * the container runner (execute a prepared spec in isolation).
// Implementations live in Application (adapters, policy) and Infrastructure
// (Docker runner). The web app never talks to Docker directly.
// ---------------------------------------------------------------------------

/// <summary>Resource and isolation limits applied to a tool container.</summary>
public sealed record ExecutionLimits(
    int TimeoutSeconds,
    long MemoryBytes,
    double Cpus,
    int PidsLimit,
    ContainerNetwork Network)
{
    public static ExecutionLimits Default { get; } =
        new(TimeoutSeconds: 300, MemoryBytes: 512L * 1024 * 1024, Cpus: 1.0, PidsLimit: 256, Network: ContainerNetwork.Bridge);
}

/// <summary>
/// Static description of a tool: its identity, the pinned approved image, its
/// risk class, execution limits and a human-readable capability summary.
/// </summary>
public sealed record ToolDefinition(
    string Slug,
    string DisplayName,
    string ImageRef,
    ToolRiskLevel Risk,
    ExecutionLimits Limits,
    string Capabilities)
{
    public bool IsActiveScan => Risk == ToolRiskLevel.Active;
}

/// <summary>Result of building an argument vector from approved parameters.</summary>
public sealed record ArgumentBuildResult(bool Ok, IReadOnlyList<string> Arguments, string? Error)
{
    public static ArgumentBuildResult Success(IReadOnlyList<string> args) => new(true, args, null);
    public static ArgumentBuildResult Failure(string error) => new(false, Array.Empty<string>(), error);
}

/// <summary>
/// Structured, generic parse output rendered as a titled table in the UI and
/// stored as JSON. Deliberately neutral — nothing here is a "confirmed vuln".
/// </summary>
public sealed record ToolParseResult(
    string Title,
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string Summary)
{
    public static ToolParseResult Empty(string title, string summary) =>
        new(title, Array.Empty<string>(), Array.Empty<IReadOnlyList<string>>(), summary);
}

/// <summary>
/// A tool adapter: everything needed to run and interpret ONE tool. Pure and
/// side-effect free — it builds argv and parses output but never executes.
/// </summary>
public interface IToolAdapter
{
    /// <summary>Slug matching <see cref="SecurityTool.Slug"/> and <see cref="ToolDefinition.Slug"/>.</summary>
    string Slug { get; }

    ToolDefinition Definition { get; }

    /// <summary>
    /// Validates the approved parameter set against this tool's allowed schema and
    /// builds the argument vector, injecting only the given (already-authorized)
    /// target. Rejects unknown/malformed parameters — the command-injection gate.
    /// </summary>
    ArgumentBuildResult BuildArguments(string targetValue, IReadOnlyDictionary<string, string> parameters);

    /// <summary>Parses raw tool output into a structured, neutral result.</summary>
    ToolParseResult Parse(string stdout, string stderr);
}

/// <summary>Everything the policy needs to make an authorization decision.</summary>
public sealed record ToolAuthorizationContext(
    Project Project,
    ScopeTarget Target,
    ToolDefinition Tool,
    bool ConfirmedActive);

/// <summary>Authorization gate for execution. Deny-by-default in every implementation.</summary>
public interface IToolExecutionPolicy
{
    ToolExecutionAuthorization Authorize(ToolAuthorizationContext context);
}

/// <summary>Result of an authorization check, with a recorded reason.</summary>
public sealed record ToolExecutionAuthorization(bool IsAllowed, string Reason)
{
    public static ToolExecutionAuthorization Allow() => new(true, "Authorized.");
    public static ToolExecutionAuthorization Deny(string reason) => new(false, reason);
}

/// <summary>A fully-prepared, authorized request to run one tool container.</summary>
public sealed record ContainerRunSpec(
    int RunId,
    string ToolSlug,
    string ImageRef,
    string TargetValue,
    IReadOnlyList<string> Arguments,
    ExecutionLimits Limits);

/// <summary>Outcome of a container run (logs are streamed via a callback, not returned).</summary>
public sealed record ContainerRunResult(
    int ExitCode,
    bool TimedOut,
    bool Cancelled,
    string Error)
{
    public bool Succeeded => ExitCode == 0 && !TimedOut && !Cancelled && string.IsNullOrEmpty(Error);
}

/// <summary>
/// Executes a prepared <see cref="ContainerRunSpec"/> in an isolated ephemeral
/// container, streaming log lines through <paramref name="onLog"/>. The only
/// component granted Docker access; its interface upward is intentionally narrow.
/// </summary>
public interface IToolRunner
{
    Task<ContainerRunResult> RunAsync(
        ContainerRunSpec spec,
        Func<LogStream, string, Task> onLog,
        CancellationToken cancellationToken = default);
}
