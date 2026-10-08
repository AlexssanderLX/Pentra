namespace Pentra.Domain.Abstractions;

/// <summary>
/// Abstraction over the current time, so services and auditing stay testable.
/// </summary>
public interface ISystemClock
{
    /// <summary>The current UTC time.</summary>
    DateTime UtcNow { get; }
}
