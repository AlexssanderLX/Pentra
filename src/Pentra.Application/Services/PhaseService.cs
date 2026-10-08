using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Domain.Entities;

namespace Pentra.Application.Services;

public sealed class PhaseService : IPhaseService
{
    private readonly IPentraDbContext _db;

    public PhaseService(IPentraDbContext db) => _db = db;

    public async Task<IReadOnlyList<PentestPhase>> GetPhasesAsync(CancellationToken ct = default) =>
        await _db.Phases
            .AsNoTracking()
            .OrderBy(p => p.Order)
            .ToListAsync(ct);
}
