using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Execution;
using Pentra.Application.Execution.Adapters;
using Pentra.Application.Security;
using Pentra.Application.Services;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class ExecutionEngineTests
{
    private const string NmapXml = """
    <?xml version="1.0"?>
    <nmaprun><host><address addr="1.2.3.4" addrtype="ipv4"/>
    <ports><port protocol="tcp" portid="80"><state state="open"/><service name="http"/></port></ports>
    </host></nmaprun>
    """;

    private static (ExecutionService svc, ExecutionQueue queue, ToolAdapterRegistry reg, ChangeHistoryService hist) Wire(TestHarness h)
    {
        var hist = new ChangeHistoryService(h.Db, h.Clock);
        var reg = new ToolAdapterRegistry(new IToolAdapter[] { new NmapAdapter(), new SubfinderAdapter(), new HttpxAdapter() });
        var svc = new ExecutionService(h.Db, h.Clock, hist, reg, new ScopedToolExecutionPolicy());
        var queue = new ExecutionQueue(h.Db, h.Clock);
        return (svc, queue, reg, hist);
    }

    private static ExecutionEngine Engine(TestHarness h, ToolAdapterRegistry reg, ExecutionQueue queue, ChangeHistoryService hist, FakeToolRunner runner) =>
        new(h.Db, reg, new ScopedToolExecutionPolicy(), runner, queue, hist);

    private static async Task<int> QueueNmapRunAsync(TestHarness h, ExecutionService svc, RunScenario s)
    {
        var r = await svc.RequestRunAsync(new RequestRunInput(s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, new Dictionary<string, string>(), ConfirmedActive: true));
        r.Succeeded.Should().BeTrue();
        return r.Value;
    }

    [Fact]
    public async Task Process_CompletesSuccessfully_AndParsesAndLogs()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var (svc, queue, reg, hist) = Wire(h);
        var runId = await QueueNmapRunAsync(h, svc, s);
        (await queue.ClaimNextAsync("t1")).Should().Be(runId);
        h.Db.ChangeTracker.Clear();

        var runner = new FakeToolRunner
        {
            Behavior = async (_, onLog, _) =>
            {
                await onLog(LogStream.Stdout, NmapXml);
                return new ContainerRunResult(0, false, false, "");
            }
        };

        await Engine(h, reg, queue, hist, runner).ProcessAsync(runId, CancellationToken.None);

        var run = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
        run.Status.Should().Be(ExecutionStatus.Completed);
        run.ExitCode.Should().Be(0);
        run.DurationMs.Should().NotBeNull();
        run.ResultJson.Should().Contain("Open ports");
        run.ArtifactSha256.Should().NotBeNullOrWhiteSpace();
        (await h.Db.ToolRunLogs.CountAsync(l => l.ToolRunId == runId)).Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Process_MarksFailed_OnNonZeroExit()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var (svc, queue, reg, hist) = Wire(h);
        var runId = await QueueNmapRunAsync(h, svc, s);
        await queue.ClaimNextAsync("t1");
        h.Db.ChangeTracker.Clear();

        var runner = new FakeToolRunner
        {
            Behavior = (_, _, _) => Task.FromResult(new ContainerRunResult(1, false, false, "boom"))
        };

        await Engine(h, reg, queue, hist, runner).ProcessAsync(runId, CancellationToken.None);

        var run = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
        run.Status.Should().Be(ExecutionStatus.Failed);
        run.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Process_MarksCancelled_WhenCancelRequested()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var (svc, queue, reg, hist) = Wire(h);
        var runId = await QueueNmapRunAsync(h, svc, s);
        await queue.ClaimNextAsync("t1");
        h.Db.ChangeTracker.Clear();

        // Ask for cancellation up front; the engine's poll will observe it.
        (await svc.RequestCancelAsync(runId)).Succeeded.Should().BeTrue();

        var runner = new FakeToolRunner
        {
            Behavior = async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct); // runs until cancelled
                return new ContainerRunResult(0, false, false, "");
            }
        };

        await Engine(h, reg, queue, hist, runner).ProcessAsync(runId, CancellationToken.None);

        var run = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
        run.Status.Should().Be(ExecutionStatus.Cancelled);
    }

    [Fact]
    public async Task Process_MarksTimedOut_WhenTimeoutElapses()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var (svc, queue, reg, hist) = Wire(h);
        var runId = await QueueNmapRunAsync(h, svc, s);
        await queue.ClaimNextAsync("t1");
        h.Db.ChangeTracker.Clear();

        // Shrink the timeout for a fast test.
        var run = await h.Db.ToolRuns.FirstAsync(r => r.Id == runId);
        run.TimeoutSeconds = 1;
        await h.Db.SaveChangesAsync();

        var runner = new FakeToolRunner
        {
            Behavior = async (_, _, ct) =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                return new ContainerRunResult(0, false, false, "");
            }
        };

        await Engine(h, reg, queue, hist, runner).ProcessAsync(runId, CancellationToken.None);

        var reloaded = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == runId);
        reloaded.Status.Should().Be(ExecutionStatus.TimedOut);
    }
}
