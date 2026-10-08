namespace Pentra.Domain.Enums;

/// <summary>
/// Risk classification of a tool. Active scans touch the target and require
/// explicit operator confirmation; passive ones do not connect to the target.
/// </summary>
public enum ToolRiskLevel
{
    Passive = 0,
    Active = 1
}
