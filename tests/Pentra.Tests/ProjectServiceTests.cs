using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Pentra.Application.Models;
using Pentra.Application.Services;
using Pentra.Domain.Enums;
using Pentra.Tests.TestSupport;
using Xunit;

namespace Pentra.Tests;

public class ProjectServiceTests
{
    private static ProjectService CreateSut(TestHarness h)
    {
        var history = new ChangeHistoryService(h.Db, h.Clock);
        return new ProjectService(h.Db, h.Clock, history);
    }

    private static CreateProjectRequest ValidCreate(string name = "Acme External") =>
        new(name, "Acme Corp", "desc", ProjectStatus.Scoping, "authorized by MSA", null, null);

    [Fact]
    public async Task CreateAsync_PersistsProject_AndRecordsHistory()
    {
        using var h = new TestHarness();
        var sut = CreateSut(h);

        var result = await sut.CreateAsync(ValidCreate());

        result.Succeeded.Should().BeTrue();
        var project = await h.Db.Projects.SingleAsync();
        project.Name.Should().Be("Acme External");
        project.CreatedAt.Should().Be(h.Clock.UtcNow);

        var history = await h.Db.History.ToListAsync();
        history.Should().ContainSingle(e => e.Action == ChangeAction.Created);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenNameMissing()
    {
        using var h = new TestHarness();
        var sut = CreateSut(h);

        var result = await sut.CreateAsync(ValidCreate(name: "   "));

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        (await h.Db.Projects.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_Fails_WhenEndBeforeStart()
    {
        using var h = new TestHarness();
        var sut = CreateSut(h);

        var request = ValidCreate() with
        {
            StartDate = new DateOnly(2026, 5, 10),
            EndDate = new DateOnly(2026, 5, 1)
        };

        var result = await sut.CreateAsync(request);

        result.Succeeded.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RecordsStatusChange()
    {
        using var h = new TestHarness();
        var sut = CreateSut(h);
        var created = await sut.CreateAsync(ValidCreate());
        var id = created.Value;

        var result = await sut.UpdateAsync(new UpdateProjectRequest(
            id, "Acme External", "Acme Corp", "desc", ProjectStatus.Active, "notes", null, null));

        result.Succeeded.Should().BeTrue();
        (await h.Db.Projects.SingleAsync()).Status.Should().Be(ProjectStatus.Active);
        (await h.Db.History.CountAsync(e => e.Action == ChangeAction.StatusChanged)).Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsync_RemovesProject()
    {
        using var h = new TestHarness();
        var sut = CreateSut(h);
        var id = (await sut.CreateAsync(ValidCreate())).Value;

        (await sut.DeleteAsync(id)).Succeeded.Should().BeTrue();
        (await h.Db.Projects.CountAsync()).Should().Be(0);
    }
}
