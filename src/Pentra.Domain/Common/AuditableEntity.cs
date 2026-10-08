namespace Pentra.Domain.Common;

/// <summary>
/// Base type for persisted entities that track creation and last-modification timestamps.
/// Timestamps are assigned by the persistence layer through an <see cref="Pentra.Domain.Abstractions.ISystemClock"/>.
/// </summary>
public abstract class AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Creation timestamp in UTC. Stored as UTC <see cref="DateTime"/> for portable, sortable SQLite storage.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last-modification timestamp in UTC, or null if never modified.</summary>
    public DateTime? UpdatedAt { get; set; }
}
