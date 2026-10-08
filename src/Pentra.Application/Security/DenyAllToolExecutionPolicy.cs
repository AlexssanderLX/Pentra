using Pentra.Domain.Abstractions;

namespace Pentra.Application.Security;

/// <summary>
/// Checkpoint 1 execution policy: denies every request unconditionally.
/// No scanner runs in this version — this is the enforced default, and the
/// concrete gate that later checkpoints will replace with real authorization
/// (in-scope + explicitly authorized target + permitted project state).
/// </summary>
public sealed class DenyAllToolExecutionPolicy : IToolExecutionPolicy
{
    public ToolExecutionAuthorization Authorize(ToolExecutionRequest request) =>
        ToolExecutionAuthorization.Deny(
            "Tool execution is disabled in this checkpoint. No scanners are run by Pentra at this stage.");
}
