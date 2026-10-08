using FluentAssertions;
using Pentra.Application.Execution.Adapters;
using Pentra.Application.Security;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;
using Xunit;

namespace Pentra.Tests;

public class ScopedPolicyTests
{
    private static readonly ToolDefinition ActiveTool = new NmapAdapter().Definition;      // Active
    private static readonly ToolDefinition PassiveTool = new SubfinderAdapter().Definition; // Passive

    private static Project ActiveProject(int id = 1) => new() { Id = id, Status = ProjectStatus.Active };

    private static ScopeTarget AuthorizedTarget(int projectId = 1) =>
        new() { ProjectId = projectId, Value = "example.com", IsAuthorized = true };

    private readonly ScopedToolExecutionPolicy _policy = new();

    [Fact]
    public void Allows_WhenActiveProject_AuthorizedTarget_And_ActiveConfirmed()
    {
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            ActiveProject(), AuthorizedTarget(), ActiveTool, ConfirmedActive: true));

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Allows_PassiveTool_WithoutActiveConfirmation()
    {
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            ActiveProject(), AuthorizedTarget(), PassiveTool, ConfirmedActive: false));

        decision.IsAllowed.Should().BeTrue();
    }

    [Fact]
    public void Denies_WhenProjectNotActive()
    {
        var project = new Project { Id = 1, Status = ProjectStatus.Scoping };
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            project, AuthorizedTarget(), ActiveTool, ConfirmedActive: true));

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Contain("Active");
    }

    [Fact]
    public void Denies_WhenTargetNotAuthorized()
    {
        var target = new ScopeTarget { ProjectId = 1, Value = "example.com", IsAuthorized = false };
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            ActiveProject(), target, ActiveTool, ConfirmedActive: true));

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Contain("authorized");
    }

    [Fact]
    public void Denies_WhenTargetBelongsToAnotherProject()
    {
        var target = AuthorizedTarget(projectId: 99);
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            ActiveProject(1), target, ActiveTool, ConfirmedActive: true));

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Contain("scope");
    }

    [Fact]
    public void Denies_ActiveScan_WithoutConfirmation()
    {
        var decision = _policy.Authorize(new ToolAuthorizationContext(
            ActiveProject(), AuthorizedTarget(), ActiveTool, ConfirmedActive: false));

        decision.IsAllowed.Should().BeFalse();
        decision.Reason.Should().Contain("confirmation");
    }
}
