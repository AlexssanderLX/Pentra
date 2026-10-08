using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution;

/// <summary>
/// Drives a single claimed run to completion: re-authorizes (defense in depth),
/// builds the container spec, executes it through the runner while streaming
/// logs, then parses and persists the result. Scanner-agnostic — all
/// tool-specific logic lives in adapters and the runner.
/// </summary>
public interface IExecutionEngine
{
    Task ProcessAsync(int runId, CancellationToken shutdownToken);
}

public sealed class ExecutionEngine : IExecutionEngine
{
    private readonly IPentraDbContext _db;
    private readonly IToolAdapterRegistry _adapters;
    private readonly IToolExecutionPolicy _policy;
    private readonly IToolRunner _runner;
    private readonly IExecutionQueue _queue;
    private readonly IChangeHistoryService _history;

    public ExecutionEngine(
        IPentraDbContext db,
        IToolAdapterRegistry adapters,
        IToolExecutionPolicy policy,
        IToolRunner runner,
        IExecutionQueue queue,
        IChangeHistoryService history)
    {
        _db = db;
        _adapters = adapters;
        _policy = policy;
        _runner = runner;
        _queue = queue;
        _history = history;
    }

    public async Task ProcessAsync(int runId, CancellationToken shutdownToken)
    {
        var run = await _db.ToolRuns
            .Include(r => r.Project)
            .Include(r => r.ScopeTarget)
            .FirstOrDefaultAsync(r => r.Id == runId, shutdownToken);

        if (run is null || run.Status != ExecutionStatus.Running)
        {
            return;
        }

        // --- Re-authorize (defense in depth) --------------------------------
        if (!_adapters.TryGet(run.ToolSlug, out var adapter))
        {
            await FailAsync(run, "No adapter is registered for this tool.", shutdownToken);
            return;
        }

        if (run.Project is null || run.ScopeTarget is null)
        {
            await FailAsync(run, "Project or target is no longer available.", shutdownToken);
            return;
        }

        var decision = _policy.Authorize(new ToolAuthorizationContext(run.Project, run.ScopeTarget, adapter.Definition, run.ConfirmedActive));
        if (!decision.IsAllowed)
        {
            await FailAsync(run, $"Authorization revoked: {decision.Reason}", shutdownToken);
            return;
        }

        // --- Build the container spec ---------------------------------------
        var args = JsonSerializer.Deserialize<List<string>>(run.ApprovedArgumentsJson) ?? new List<string>();
        var limits = adapter.Definition.Limits with { TimeoutSeconds = run.TimeoutSeconds };
        var spec = new ContainerRunSpec(run.Id, run.ToolSlug, run.ImageRef, run.TargetValue, args, limits);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        async Task OnLog(LogStream stream, string text)
        {
            (stream == LogStream.Stderr ? stderr : stdout).AppendLine(text);
            await _queue.AppendLogAsync(run.Id, stream, text, CancellationToken.None);
        }

        await _queue.AppendLogAsync(run.Id, LogStream.System, $"Starting {adapter.Definition.DisplayName} ({run.ImageRef}) against {run.TargetValue}", shutdownToken);

        // --- Timeout + cooperative cancellation -----------------------------
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, run.TimeoutSeconds)));
        using var cancelCts = new CancellationTokenSource();
        using var pollStopCts = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(shutdownToken, timeoutCts.Token, cancelCts.Token);

        var cancelPoll = PollForCancellationAsync(run.Id, cancelCts, pollStopCts.Token);

        ContainerRunResult result;
        try
        {
            result = await _runner.RunAsync(spec, OnLog, linked.Token);
        }
        catch (OperationCanceledException)
        {
            result = new ContainerRunResult(-1, TimedOut: timeoutCts.IsCancellationRequested, Cancelled: cancelCts.IsCancellationRequested, Error: "Execution stopped.");
        }
        catch (Exception ex)
        {
            result = new ContainerRunResult(-1, TimedOut: false, Cancelled: false, Error: ex.Message);
        }
        finally
        {
            // Stop the cancellation poll regardless of how the run ended.
            pollStopCts.Cancel();
        }

        // --- Classify final state -------------------------------------------
        var status =
            cancelCts.IsCancellationRequested ? ExecutionStatus.Cancelled
            : timeoutCts.IsCancellationRequested ? ExecutionStatus.TimedOut
            : result.Succeeded ? ExecutionStatus.Completed
            : ExecutionStatus.Failed;

        var parsed = adapter.Parse(stdout.ToString(), stderr.ToString());
        var resultJson = JsonSerializer.Serialize(parsed);

        await _queue.AppendLogAsync(run.Id, LogStream.System, $"Finished with status {status} (exit {result.ExitCode}). {parsed.Summary}", shutdownToken);

        await _queue.CompleteAsync(run.Id, new ExecutionCompletion(
            status,
            result.ExitCode,
            stdout.ToString(),
            stderr.ToString(),
            resultJson,
            ExecutionText.Sha256Hex(stdout.ToString()),
            status is ExecutionStatus.Failed or ExecutionStatus.TimedOut ? (string.IsNullOrEmpty(result.Error) ? status.ToString() : result.Error) : string.Empty),
            shutdownToken);

        await SafeAwait(cancelPoll);

        await RecordHistoryAsync(run.ProjectId, status, adapter.Definition.DisplayName, run.TargetValue, shutdownToken);
    }

    private async Task PollForCancellationAsync(int runId, CancellationTokenSource cancelCts, CancellationToken stopToken)
    {
        try
        {
            while (!stopToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stopToken);
                if (await _queue.IsCancelRequestedAsync(runId, CancellationToken.None))
                {
                    cancelCts.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown of the poll loop
        }
    }

    private async Task FailAsync(ToolRun run, string reason, CancellationToken ct)
    {
        await _queue.AppendLogAsync(run.Id, LogStream.System, reason, ct);
        await _queue.CompleteAsync(run.Id, new ExecutionCompletion(
            ExecutionStatus.Failed, null, run.RawOutput, run.ErrorOutput, run.ResultJson, run.ArtifactSha256, reason), ct);
        await RecordHistoryAsync(run.ProjectId, ExecutionStatus.Failed, run.ToolSlug, run.TargetValue, ct);
    }

    private async Task RecordHistoryAsync(int projectId, ExecutionStatus status, string tool, string target, CancellationToken ct)
    {
        var action = status switch
        {
            ExecutionStatus.Completed => ChangeAction.ExecutionCompleted,
            ExecutionStatus.Cancelled => ChangeAction.ExecutionCancelled,
            _ => ChangeAction.ExecutionFailed
        };
        await _history.RecordAsync(projectId, action, nameof(ToolRun), $"{tool} run on '{target}' finished: {status}.", ct);
        await _db.SaveChangesAsync(ct);
    }

    private static async Task SafeAwait(Task task)
    {
        try { await task; } catch { /* poll task teardown */ }
    }
}
