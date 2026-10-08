using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Application.Common;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution;

/// <summary>Web-facing operations for requesting, cancelling and querying runs.</summary>
public interface IExecutionService
{
    Task<Result<int>> RequestRunAsync(RequestRunInput input, CancellationToken ct = default);
    Task<Result> RequestCancelAsync(int runId, CancellationToken ct = default);
    Task<ToolRun?> GetRunAsync(int runId, CancellationToken ct = default);
    Task<IReadOnlyList<ToolRun>> GetRunsForProjectAsync(int projectId, CancellationToken ct = default);
    Task<IReadOnlyList<ToolRunLogLine>> GetLogDeltaAsync(int runId, int afterSeq, CancellationToken ct = default);
}

public sealed class ExecutionService : IExecutionService
{
    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;
    private readonly IChangeHistoryService _history;
    private readonly IToolAdapterRegistry _adapters;
    private readonly IToolExecutionPolicy _policy;

    public ExecutionService(
        IPentraDbContext db,
        ISystemClock clock,
        IChangeHistoryService history,
        IToolAdapterRegistry adapters,
        IToolExecutionPolicy policy)
    {
        _db = db;
        _clock = clock;
        _history = history;
        _adapters = adapters;
        _policy = policy;
    }

    public async Task<Result<int>> RequestRunAsync(RequestRunInput input, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == input.ProjectId, ct);
        if (project is null)
        {
            return Result<int>.Failure("Project not found.");
        }

        var phaseExists = await _db.Phases.AnyAsync(p => p.Id == input.PhaseId, ct);
        if (!phaseExists)
        {
            return Result<int>.Failure("Phase not found.");
        }

        var tool = await _db.Tools.FirstOrDefaultAsync(t => t.Id == input.SecurityToolId, ct);
        if (tool is null)
        {
            return Result<int>.Failure("Tool not found.");
        }

        var target = await _db.Targets.FirstOrDefaultAsync(t => t.Id == input.ScopeTargetId, ct);
        if (target is null || target.ProjectId != input.ProjectId)
        {
            return Result<int>.Failure("Target not found in this project's scope.");
        }

        if (!_adapters.TryGet(tool.Slug, out var adapter))
        {
            return Result<int>.Failure($"'{tool.Name}' cannot be executed yet (no adapter).");
        }

        var parameters = input.Parameters ?? new Dictionary<string, string>();

        // Build argv (validates parameters + target; the injection gate).
        var build = adapter.BuildArguments(target.Value, parameters);
        if (!build.Ok)
        {
            return Result<int>.Failure(build.Error ?? "Invalid parameters.");
        }

        // Authorize (deny-by-default). Record the decision either way.
        var decision = _policy.Authorize(new ToolAuthorizationContext(project, target, adapter.Definition, input.ConfirmedActive));
        if (!decision.IsAllowed)
        {
            await _history.RecordAsync(project.Id, ChangeAction.ExecutionDenied, nameof(ToolRun),
                $"Denied {adapter.Definition.DisplayName} on '{target.Value}': {decision.Reason}", ct);
            await _db.SaveChangesAsync(ct);
            return Result<int>.Failure(decision.Reason);
        }

        var parametersJson = CanonicalJson(parameters);

        // Duplicate guard: no identical active run already queued/running.
        var duplicate = await _db.ToolRuns.AnyAsync(r =>
            r.ProjectId == input.ProjectId &&
            r.PhaseId == input.PhaseId &&
            r.SecurityToolId == input.SecurityToolId &&
            r.ScopeTargetId == input.ScopeTargetId &&
            r.ParametersJson == parametersJson &&
            (r.Status == ExecutionStatus.Pending || r.Status == ExecutionStatus.Running), ct);
        if (duplicate)
        {
            return Result<int>.Failure("An identical run is already pending or running.");
        }

        var run = new ToolRun
        {
            ProjectId = input.ProjectId,
            PhaseId = input.PhaseId,
            SecurityToolId = input.SecurityToolId,
            ScopeTargetId = input.ScopeTargetId,
            ToolSlug = tool.Slug,
            ImageRef = adapter.Definition.ImageRef,
            TargetValue = target.Value,
            ParametersJson = parametersJson,
            ApprovedArgumentsJson = JsonSerializer.Serialize(build.Arguments),
            IsActiveScan = adapter.Definition.IsActiveScan,
            ConfirmedActive = input.ConfirmedActive,
            AuthorizationAllowed = true,
            AuthorizationReason = decision.Reason,
            TimeoutSeconds = adapter.Definition.Limits.TimeoutSeconds,
            Status = ExecutionStatus.Pending,
            CreatedAt = _clock.UtcNow
        };

        _db.ToolRuns.Add(run);
        await _history.RecordAsync(project.Id, ChangeAction.ExecutionRequested, nameof(ToolRun),
            $"Requested {adapter.Definition.DisplayName} on '{target.Value}'.", ct);
        await _db.SaveChangesAsync(ct);

        return Result<int>.Success(run.Id);
    }

    public async Task<Result> RequestCancelAsync(int runId, CancellationToken ct = default)
    {
        var run = await _db.ToolRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return Result.Failure("Run not found.");
        }

        if (!run.IsActive)
        {
            return Result.Failure("Run is not active.");
        }

        run.CancelRequested = true;
        run.UpdatedAt = _clock.UtcNow;

        // If still only Pending (never claimed), cancel immediately.
        if (run.Status == ExecutionStatus.Pending)
        {
            run.Status = ExecutionStatus.Cancelled;
            run.CompletedAt = _clock.UtcNow;
            await _history.RecordAsync(run.ProjectId, ChangeAction.ExecutionCancelled, nameof(ToolRun),
                $"Cancelled pending run on '{run.TargetValue}'.", ct);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public Task<ToolRun?> GetRunAsync(int runId, CancellationToken ct = default) =>
        _db.ToolRuns
            .AsNoTracking()
            .Include(r => r.SecurityTool)
            .Include(r => r.Phase)
            .FirstOrDefaultAsync(r => r.Id == runId, ct);

    public async Task<IReadOnlyList<ToolRun>> GetRunsForProjectAsync(int projectId, CancellationToken ct = default) =>
        await _db.ToolRuns
            .AsNoTracking()
            .Where(r => r.ProjectId == projectId)
            .Include(r => r.SecurityTool)
            .Include(r => r.Phase)
            .OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ToolRunLogLine>> GetLogDeltaAsync(int runId, int afterSeq, CancellationToken ct = default) =>
        await _db.ToolRunLogs
            .AsNoTracking()
            .Where(l => l.ToolRunId == runId && l.Seq > afterSeq)
            .OrderBy(l => l.Seq)
            .ToListAsync(ct);

    /// <summary>Stable JSON for a parameter set (sorted keys) for storage and dedup.</summary>
    private static string CanonicalJson(IReadOnlyDictionary<string, string> parameters)
    {
        var sorted = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in parameters)
        {
            sorted[kv.Key] = kv.Value;
        }
        return JsonSerializer.Serialize(sorted);
    }
}
