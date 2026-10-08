using Pentra.Domain.Common;
using Pentra.Domain.Enums;

namespace Pentra.Domain.Entities;

/// <summary>
/// An authorized target within a project's scope. Only targets explicitly
/// marked <see cref="IsAuthorized"/> may ever be acted upon by future runners.
/// </summary>
public class ScopeTarget : AuditableEntity
{
    public int ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>The raw target value (domain, IP, CIDR or URL).</summary>
    public string Value { get; set; } = string.Empty;

    public TargetKind Kind { get; set; }

    /// <summary>
    /// Explicit authorization flag. Scope safety gate for future execution:
    /// nothing may target an entry where this is false.
    /// </summary>
    public bool IsAuthorized { get; set; }

    public string Notes { get; set; } = string.Empty;
}
