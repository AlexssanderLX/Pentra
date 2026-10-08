using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Services;

public sealed class ChangeHistoryService : IChangeHistoryService
{
    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;

    public ChangeHistoryService(IPentraDbContext db, ISystemClock clock)
    {
        _db = db;
        _clock = clock;
    }

    /// <summary>
    /// Appends an entry. Does not call SaveChanges so it can participate in the
    /// caller's unit of work (the originating change and its history row commit
    /// together).
    /// </summary>
    public Task RecordAsync(int projectId, ChangeAction action, string entityType, string summary, CancellationToken ct = default)
    {
        _db.History.Add(new ChangeHistoryEntry
        {
            ProjectId = projectId,
            Action = action,
            EntityType = entityType,
            Summary = summary,
            CreatedAt = _clock.UtcNow
        });

        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<ChangeHistoryEntry>> GetForProjectAsync(int projectId, CancellationToken ct = default) =>
        await _db.History
            .Where(h => h.ProjectId == projectId)
            .OrderByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .ToListAsync(ct);
}
