using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Execution;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;
using Pentra.Infrastructure.Persistence;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class ExecutionQueueTests
{
    private static async Task<int> AddPendingRunAsync(TestHarness h, int projectId)
    {
        var phaseId = await h.Db.Phases.OrderBy(p => p.Order).Select(p => p.Id).FirstAsync();
        var toolId = await h.Db.Tools.Select(t => t.Id).FirstAsync();

        var run = new ToolRun
        {
            ProjectId = projectId,
            PhaseId = phaseId,
            SecurityToolId = toolId,
            ToolSlug = "nmap",
            TargetValue = "example.com",
            TimeoutSeconds = 60,
            Status = ExecutionStatus.Pending,
            CreatedAt = h.Clock.UtcNow
        };
        h.Db.ToolRuns.Add(run);
        await h.Db.SaveChangesAsync();
        return run.Id;
    }

    private static async Task<int> SeedProjectAsync(TestHarness h)
    {
        await DataSeeder.SeedAsync(h.Db, h.Clock);
        var p = new Project { Name = "Q", Client = "c", Status = ProjectStatus.Active, CreatedAt = h.Clock.UtcNow };
        h.Db.Projects.Add(p);
        await h.Db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task ClaimNext_ClaimsEachRunOnce_AndReturnsNullWhenEmpty()
    {
        using var h = new TestHarness();
        var pid = await SeedProjectAsync(h);
        var id1 = await AddPendingRunAsync(h, pid);
        var id2 = await AddPendingRunAsync(h, pid);
        var queue = new ExecutionQueue(h.Db, h.Clock);

        var first = await queue.ClaimNextAsync("runner-a");
        var second = await queue.ClaimNextAsync("runner-a");
        var third = await queue.ClaimNextAsync("runner-a");

        new[] { first, second }.Should().BeEquivalentTo(new[] { (int?)id1, id2 });
        third.Should().BeNull();
        (await h.Db.ToolRuns.CountAsync(r => r.Status == ExecutionStatus.Running)).Should().Be(2);
    }

    [Fact]
    public async Task Claim_SetsRunningMetadata()
    {
        using var h = new TestHarness();
        var pid = await SeedProjectAsync(h);
        var id = await AddPendingRunAsync(h, pid);
        var queue = new ExecutionQueue(h.Db, h.Clock);

        await queue.ClaimNextAsync("runner-x");

        var run = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == id);
        run.Status.Should().Be(ExecutionStatus.Running);
        run.RunnerId.Should().Be("runner-x");
        run.StartedAt.Should().NotBeNull();
        run.ClaimedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RunningCount_ReflectsClaimedRuns()
    {
        using var h = new TestHarness();
        var pid = await SeedProjectAsync(h);
        await AddPendingRunAsync(h, pid);
        await AddPendingRunAsync(h, pid);
        var queue = new ExecutionQueue(h.Db, h.Clock);

        (await queue.RunningCountAsync()).Should().Be(0);
        await queue.ClaimNextAsync("r");
        (await queue.RunningCountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AppendLog_IncrementsSequence()
    {
        using var h = new TestHarness();
        var pid = await SeedProjectAsync(h);
        var id = await AddPendingRunAsync(h, pid);
        var queue = new ExecutionQueue(h.Db, h.Clock);

        await queue.AppendLogAsync(id, LogStream.Stdout, "line 1");
        await queue.AppendLogAsync(id, LogStream.Stdout, "line 2");

        var logs = await h.Db.ToolRunLogs.Where(l => l.ToolRunId == id).OrderBy(l => l.Seq).ToListAsync();
        logs.Select(l => l.Seq).Should().Equal(1, 2);
    }

    [Fact]
    public async Task RecoverInterrupted_FailsRunningRuns()
    {
        using var h = new TestHarness();
        var pid = await SeedProjectAsync(h);
        var id = await AddPendingRunAsync(h, pid);
        var queue = new ExecutionQueue(h.Db, h.Clock);
        await queue.ClaimNextAsync("r"); // now Running

        var recovered = await queue.RecoverInterruptedAsync("interrupted by restart");

        recovered.Should().Be(1);
        var run = await h.Db.ToolRuns.AsNoTracking().FirstAsync(r => r.Id == id);
        run.Status.Should().Be(ExecutionStatus.Failed);
        run.FailureReason.Should().Contain("interrupted");
    }
}
