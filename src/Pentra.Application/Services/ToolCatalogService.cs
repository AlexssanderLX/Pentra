using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Application.Common;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Services;

public sealed class ToolCatalogService : IToolCatalogService
{
    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;
    private readonly IChangeHistoryService _history;

    public ToolCatalogService(IPentraDbContext db, ISystemClock clock, IChangeHistoryService history)
    {
        _db = db;
        _clock = clock;
        _history = history;
    }

    public async Task<IReadOnlyList<SecurityTool>> GetCatalogAsync(CancellationToken ct = default) =>
        await _db.Tools
            .AsNoTracking()
            .Include(t => t.DefaultPhase)
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<Result> ToggleSelectionAsync(int projectId, int phaseId, int toolId, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(p => p.Id == projectId, ct);
        if (!projectExists)
        {
            return Result.Failure("Project not found.");
        }

        var phaseExists = await _db.Phases.AnyAsync(p => p.Id == phaseId, ct);
        var toolExists = await _db.Tools.AnyAsync(t => t.Id == toolId, ct);
        if (!phaseExists || !toolExists)
        {
            return Result.Failure("Phase or tool not found.");
        }

        var existing = await _db.ToolSelections
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.PhaseId == phaseId && s.SecurityToolId == toolId, ct);

        if (existing is not null)
        {
            _db.ToolSelections.Remove(existing);
            await _history.RecordAsync(projectId, ChangeAction.ToolDeselected, nameof(ProjectToolSelection),
                $"Tool #{toolId} deselected for phase #{phaseId}.", ct);
        }
        else
        {
            _db.ToolSelections.Add(new ProjectToolSelection
            {
                ProjectId = projectId,
                PhaseId = phaseId,
                SecurityToolId = toolId,
                CreatedAt = _clock.UtcNow
            });
            await _history.RecordAsync(projectId, ChangeAction.ToolSelected, nameof(ProjectToolSelection),
                $"Tool #{toolId} selected for phase #{phaseId}.", ct);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
