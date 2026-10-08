using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Models;
using Pentra.Application.Services;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;
using Pentra.Infrastructure.Persistence;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class TargetAndToolTests
{
    private static async Task<int> SeedProjectAsync(TestHarness h)
    {
        var history = new ChangeHistoryService(h.Db, h.Clock);
        var projects = new ProjectService(h.Db, h.Clock, history);
        var result = await projects.CreateAsync(new CreateProjectRequest(
            "Acme", "Acme Corp", "", ProjectStatus.Scoping, "", null, null));
        return result.Value;
    }

    [Fact]
    public async Task AddAsync_AddsAuthorizedTarget_AndRecordsHistory()
    {
        using var h = new TestHarness();
        var projectId = await SeedProjectAsync(h);
        var sut = new TargetService(h.Db, h.Clock, new ChangeHistoryService(h.Db, h.Clock));

        var result = await sut.AddAsync(new CreateTargetRequest(
            projectId, "example.com", TargetKind.Domain, true, "primary domain"));

        result.Succeeded.Should().BeTrue();
        var target = await h.Db.Targets.SingleAsync();
        target.IsAuthorized.Should().BeTrue();
        (await h.Db.History.CountAsync(e => e.Action == ChangeAction.TargetAdded)).Should().Be(1);
    }

    [Fact]
    public async Task AddAsync_RejectsInvalidTargetValue()
    {
        using var h = new TestHarness();
        var projectId = await SeedProjectAsync(h);
        var sut = new TargetService(h.Db, h.Clock, new ChangeHistoryService(h.Db, h.Clock));

        var result = await sut.AddAsync(new CreateTargetRequest(
            projectId, "definitely not an ip", TargetKind.IpAddress, true, ""));

        result.Succeeded.Should().BeFalse();
        (await h.Db.Targets.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicateTargetInSameProject()
    {
        using var h = new TestHarness();
        var projectId = await SeedProjectAsync(h);
        var sut = new TargetService(h.Db, h.Clock, new ChangeHistoryService(h.Db, h.Clock));

        await sut.AddAsync(new CreateTargetRequest(projectId, "example.com", TargetKind.Domain, true, ""));
        var second = await sut.AddAsync(new CreateTargetRequest(projectId, "example.com", TargetKind.Domain, true, ""));

        second.Succeeded.Should().BeFalse();
        (await h.Db.Targets.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task ToggleSelectionAsync_AddsThenRemoves()
    {
        using var h = new TestHarness();
        await DataSeeder.SeedAsync(h.Db, h.Clock);
        var projectId = await SeedProjectAsync(h);
        var phaseId = (await h.Db.Phases.FirstAsync()).Id;
        var toolId = (await h.Db.Tools.FirstAsync()).Id;

        var sut = new ToolCatalogService(h.Db, h.Clock, new ChangeHistoryService(h.Db, h.Clock));

        (await sut.ToggleSelectionAsync(projectId, phaseId, toolId)).Succeeded.Should().BeTrue();
        (await h.Db.ToolSelections.CountAsync()).Should().Be(1);

        (await sut.ToggleSelectionAsync(projectId, phaseId, toolId)).Succeeded.Should().BeTrue();
        (await h.Db.ToolSelections.CountAsync()).Should().Be(0);
    }
}
