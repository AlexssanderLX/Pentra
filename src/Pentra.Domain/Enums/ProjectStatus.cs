namespace Pentra.Domain.Enums;

/// <summary>
/// Lifecycle state of a pentest engagement.
/// </summary>
public enum ProjectStatus
{
    Draft = 0,
    Scoping = 1,
    Active = 2,
    OnHold = 3,
    Completed = 4,
    Archived = 5
}
