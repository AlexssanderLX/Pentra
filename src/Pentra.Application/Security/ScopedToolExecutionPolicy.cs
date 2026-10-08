using Pentra.Domain.Abstractions;
using Pentra.Domain.Enums;

namespace Pentra.Application.Security;

/// <summary>
/// The real execution policy: deny by default, allow only when every condition
/// holds. This is the authorization gate every run must pass before it is queued
/// and, defensively, again before the runner starts it.
/// </summary>
public sealed class ScopedToolExecutionPolicy : IToolExecutionPolicy
{
    public ToolExecutionAuthorization Authorize(ToolAuthorizationContext context)
    {
        if (context.Project is null || context.Target is null || context.Tool is null)
        {
            return ToolExecutionAuthorization.Deny("Incomplete execution request.");
        }

        // 1. The project must be in a state that permits active work.
        if (context.Project.Status != ProjectStatus.Active)
        {
            return ToolExecutionAuthorization.Deny(
                $"Project must be Active to run tools (current status: {context.Project.Status}).");
        }

        // 2. The target must belong to this project's scope.
        if (context.Target.ProjectId != context.Project.Id)
        {
            return ToolExecutionAuthorization.Deny("Target is not part of this project's scope.");
        }

        // 3. The target must be explicitly authorized.
        if (!context.Target.IsAuthorized)
        {
            return ToolExecutionAuthorization.Deny("Target is not authorized for testing.");
        }

        // 4. Active scans require explicit operator confirmation.
        if (context.Tool.IsActiveScan && !context.ConfirmedActive)
        {
            return ToolExecutionAuthorization.Deny("This is an active scan and requires explicit confirmation.");
        }

        return ToolExecutionAuthorization.Allow();
    }
}
