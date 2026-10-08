using Pentra.Domain.Abstractions;

namespace Pentra.Application.Security;

/// <summary>
/// A policy that denies every request unconditionally. No longer the default
/// (see <see cref="ScopedToolExecutionPolicy"/>); kept for tests and as a
/// safe fallback that can be wired in to globally disable execution.
/// </summary>
public sealed class DenyAllToolExecutionPolicy : IToolExecutionPolicy
{
    public ToolExecutionAuthorization Authorize(ToolAuthorizationContext context) =>
        ToolExecutionAuthorization.Deny("Tool execution is disabled by policy.");
}
