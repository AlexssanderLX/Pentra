using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Execution;
using Pentra.Application.Execution.Adapters;
using Pentra.Application.Security;
using Pentra.Application.Services;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class ExecutionServiceTests
{
    private static ExecutionService CreateSut(TestHarness h)
    {
        var history = new ChangeHistoryService(h.Db, h.Clock);
        var registry = new ToolAdapterRegistry(new IToolAdapter[] { new NmapAdapter(), new SubfinderAdapter(), new HttpxAdapter() });
        return new ExecutionService(h.Db, h.Clock, history, registry, new ScopedToolExecutionPolicy());
    }

    private static Dictionary<string, string> P() => new();

    [Fact]
    public async Task RequestRun_CreatesPendingRun_WhenAuthorizedAndConfirmed()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var sut = CreateSut(h);

        var result = await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: true));

        result.Succeeded.Should().BeTrue();
        var run = await h.Db.ToolRuns.SingleAsync();
        run.Status.Should().Be(ExecutionStatus.Pending);
        run.ToolSlug.Should().Be("nmap");
        run.ImageRef.Should().Contain("nmap");
        run.AuthorizationAllowed.Should().BeTrue();
        (await h.Db.History.AnyAsync(x => x.Action == ChangeAction.ExecutionRequested)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestRun_Denies_ActiveScanWithoutConfirmation_AndRecordsDecision()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var sut = CreateSut(h);

        var result = await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: false));

        result.Succeeded.Should().BeFalse();
        (await h.Db.ToolRuns.CountAsync()).Should().Be(0);
        (await h.Db.History.AnyAsync(x => x.Action == ChangeAction.ExecutionDenied)).Should().BeTrue();
    }

    [Fact]
    public async Task RequestRun_Denies_WhenTargetNotAuthorized()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var target = await h.Db.Targets.FirstAsync();
        target.IsAuthorized = false;
        await h.Db.SaveChangesAsync();

        var sut = CreateSut(h);
        var result = await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: true));

        result.Succeeded.Should().BeFalse();
        (await h.Db.ToolRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task RequestRun_Denies_WhenProjectNotActive()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var project = await h.Db.Projects.FirstAsync();
        project.Status = ProjectStatus.Scoping;
        await h.Db.SaveChangesAsync();

        var sut = CreateSut(h);
        var result = await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: true));

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task RequestRun_RejectsDuplicateActiveRun()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var sut = CreateSut(h);
        var input = new RequestRunInput(s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: true);

        (await sut.RequestRunAsync(input)).Succeeded.Should().BeTrue();
        var second = await sut.RequestRunAsync(input);

        second.Succeeded.Should().BeFalse();
        (await h.Db.ToolRuns.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task RequestRun_Rejects_HostileParameters()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var sut = CreateSut(h);

        var result = await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId,
            new Dictionary<string, string> { ["topPorts"] = "99999" }, ConfirmedActive: true));

        result.Succeeded.Should().BeFalse();
        (await h.Db.ToolRuns.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Cancel_PendingRun_MovesToCancelled()
    {
        using var h = new TestHarness();
        var s = await ExecutionScenario.SeedAsync(h);
        var sut = CreateSut(h);
        var runId = (await sut.RequestRunAsync(new RequestRunInput(
            s.ProjectId, s.PhaseId, s.NmapToolId, s.TargetId, P(), ConfirmedActive: true))).Value;

        var result = await sut.RequestCancelAsync(runId);

        result.Succeeded.Should().BeTrue();
        (await h.Db.ToolRuns.FindAsync(runId))!.Status.Should().Be(ExecutionStatus.Cancelled);
    }
}
