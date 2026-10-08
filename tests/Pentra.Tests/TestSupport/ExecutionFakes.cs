using Microsoft.EntityFrameworkCore;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;
using Pentra.Infrastructure.Persistence;

namespace Pentra.Tests.TestSupport;

/// <summary>A configurable in-process fake of the Docker runner for engine tests.</summary>
public sealed class FakeToolRunner : IToolRunner
{
    public Func<ContainerRunSpec, Func<LogStream, string, Task>, CancellationToken, Task<ContainerRunResult>> Behavior { get; set; }
        = async (_, onLog, _) =>
        {
            await onLog(LogStream.Stdout, "default fake output");
            return new ContainerRunResult(0, false, false, "");
        };

    public Task<ContainerRunResult> RunAsync(ContainerRunSpec spec, Func<LogStream, string, Task> onLog, CancellationToken ct) =>
        Behavior(spec, onLog, ct);
}

/// <summary>Identifiers for a ready-to-run scenario.</summary>
public sealed record RunScenario(int ProjectId, int PhaseId, int NmapToolId, int SubfinderToolId, int TargetId);

public static class ExecutionScenario
{
    /// <summary>Seeds reference data + an Active project with one authorized target.</summary>
    public static async Task<RunScenario> SeedAsync(TestHarness h, string targetValue = "scanme.example.com")
    {
        await DataSeeder.SeedAsync(h.Db, h.Clock);

        var project = new Project
        {
            Name = "Engine Test",
            Client = "Acme",
            Status = ProjectStatus.Active,
            CreatedAt = h.Clock.UtcNow
        };
        h.Db.Projects.Add(project);

        var target = new ScopeTarget
        {
            Project = project,
            Value = targetValue,
            Kind = TargetKind.Domain,
            IsAuthorized = true,
            CreatedAt = h.Clock.UtcNow
        };
        h.Db.Targets.Add(target);
        await h.Db.SaveChangesAsync();

        var phaseId = await h.Db.Phases.OrderBy(p => p.Order).Select(p => p.Id).FirstAsync();
        var nmapId = await h.Db.Tools.Where(t => t.Slug == "nmap").Select(t => t.Id).FirstAsync();
        var subfinderId = await h.Db.Tools.Where(t => t.Slug == "subfinder").Select(t => t.Id).FirstAsync();

        return new RunScenario(project.Id, phaseId, nmapId, subfinderId, target.Id);
    }
}
