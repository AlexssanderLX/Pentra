namespace Pentra.Domain.Enums;

/// <summary>Which output stream a captured log line came from.</summary>
public enum LogStream
{
    Stdout = 0,
    Stderr = 1,
    /// <summary>Engine/runner diagnostic line (not tool output).</summary>
    System = 2
}
