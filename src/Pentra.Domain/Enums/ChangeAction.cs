namespace Pentra.Domain.Enums;

/// <summary>
/// Kind of change recorded in the engagement history.
/// </summary>
public enum ChangeAction
{
    Created = 0,
    Updated = 1,
    Deleted = 2,
    StatusChanged = 3,
    TargetAdded = 4,
    TargetRemoved = 5,
    ToolSelected = 6,
    ToolDeselected = 7,
    ExecutionRequested = 8,
    ExecutionDenied = 9,
    ExecutionCancelled = 10,
    ExecutionCompleted = 11,
    ExecutionFailed = 12
}
