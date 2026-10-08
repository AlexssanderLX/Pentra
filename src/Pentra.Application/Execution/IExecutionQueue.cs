using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Execution;

/// <summary>
/// The persistence-backed job queue used by the runner. Claiming is atomic
/// (conditional status update) so multiple workers never run the same job.
/// </summary>
public interface IExecutionQueue
{
    /// <summary>Atomically claims the oldest Pending run, moving it to Running.</summary>
    Task<int?> ClaimNextAsync(string runnerId, CancellationToken ct = default);

    Task AppendLogAsync(int runId, LogStream stream, string text, CancellationToken ct = default);

    Task<bool> IsCancelRequestedAsync(int runId, CancellationToken ct = default);

    Task CompleteAsync(int runId, ExecutionCompletion completion, CancellationToken ct = default);

    /// <summary>Count of runs currently Running (for concurrency control).</summary>
    Task<int> RunningCountAsync(CancellationToken ct = default);

    /// <summary>
    /// On startup, fail any run left Running (its container died with the process),
    /// so interrupted executions do not hang forever.
    /// </summary>
    Task<int> RecoverInterruptedAsync(string reason, CancellationToken ct = default);
}

/// <summary>Final state written when a run finishes.</summary>
public sealed record ExecutionCompletion(
    ExecutionStatus Status,
    int? ExitCode,
    string RawOutput,
    string ErrorOutput,
    string ResultJson,
    string ArtifactSha256,
    string FailureReason);

public sealed class ExecutionQueue : IExecutionQueue
{
    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;

    public ExecutionQueue(IPentraDbContext db, ISystemClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<int?> ClaimNextAsync(string runnerId, CancellationToken ct = default)
    {
        // Consider a few oldest Pending candidates; claim the first we win the race for.
        var candidates = await _db.ToolRuns
            .Where(r => r.Status == ExecutionStatus.Pending)
            .OrderBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => r.Id)
            .Take(10)
            .ToListAsync(ct);

        var now = _clock.UtcNow;
        foreach (var id in candidates)
        {
            var claimed = await _db.ToolRuns
                .Where(r => r.Id == id && r.Status == ExecutionStatus.Pending)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, ExecutionStatus.Running)
                    .SetProperty(r => r.ClaimedAt, now)
                    .SetProperty(r => r.StartedAt, now)
                    .SetProperty(r => r.RunnerId, runnerId), ct);

            if (claimed == 1)
            {
                return id;
            }
        }

        return null;
    }

    public async Task AppendLogAsync(int runId, LogStream stream, string text, CancellationToken ct = default)
    {
        var nextSeq = (await _db.ToolRunLogs.Where(l => l.ToolRunId == runId).MaxAsync(l => (int?)l.Seq, ct) ?? 0) + 1;

        _db.ToolRunLogs.Add(new ToolRunLogLine
        {
            ToolRunId = runId,
            Seq = nextSeq,
            Stream = stream,
            Text = ExecutionText.Truncate(text, ExecutionText.MaxLogLineChars),
            CreatedAt = _clock.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }

    public Task<bool> IsCancelRequestedAsync(int runId, CancellationToken ct = default) =>
        _db.ToolRuns.Where(r => r.Id == runId).Select(r => r.CancelRequested).FirstOrDefaultAsync(ct);

    public async Task CompleteAsync(int runId, ExecutionCompletion completion, CancellationToken ct = default)
    {
        var run = await _db.ToolRuns.FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        run.Status = completion.Status;
        run.ExitCode = completion.ExitCode;
        run.RawOutput = ExecutionText.Truncate(completion.RawOutput, ExecutionText.MaxOutputChars);
        run.ErrorOutput = ExecutionText.Truncate(completion.ErrorOutput, ExecutionText.MaxOutputChars);
        run.ResultJson = completion.ResultJson;
        run.ArtifactSha256 = completion.ArtifactSha256;
        run.FailureReason = completion.FailureReason;
        run.CompletedAt = now;
        run.DurationMs = run.StartedAt.HasValue ? (long)(now - run.StartedAt.Value).TotalMilliseconds : null;
        run.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);
    }

    public Task<int> RunningCountAsync(CancellationToken ct = default) =>
        _db.ToolRuns.CountAsync(r => r.Status == ExecutionStatus.Running, ct);

    public async Task<int> RecoverInterruptedAsync(string reason, CancellationToken ct = default)
    {
        var now = _clock.UtcNow;
        return await _db.ToolRuns
            .Where(r => r.Status == ExecutionStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ExecutionStatus.Failed)
                .SetProperty(r => r.FailureReason, reason)
                .SetProperty(r => r.CompletedAt, now)
                .SetProperty(r => r.UpdatedAt, now), ct);
    }
}
