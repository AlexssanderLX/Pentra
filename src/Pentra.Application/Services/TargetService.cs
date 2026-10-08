using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Application.Common;
using Pentra.Application.Models;
using Pentra.Application.Validation;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Services;

public sealed class TargetService : ITargetService
{
    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;
    private readonly IChangeHistoryService _history;

    public TargetService(IPentraDbContext db, ISystemClock clock, IChangeHistoryService history)
    {
        _db = db;
        _clock = clock;
        _history = history;
    }

    public async Task<Result<int>> AddAsync(CreateTargetRequest request, CancellationToken ct = default)
    {
        var validationError = ScopeValidator.Validate(request.Kind, request.Value);
        if (validationError is not null)
        {
            return Result<int>.Failure(validationError);
        }

        var projectExists = await _db.Projects.AnyAsync(p => p.Id == request.ProjectId, ct);
        if (!projectExists)
        {
            return Result<int>.Failure("Project not found.");
        }

        var value = request.Value.Trim();

        var duplicate = await _db.Targets
            .AnyAsync(t => t.ProjectId == request.ProjectId && t.Value == value, ct);
        if (duplicate)
        {
            return Result<int>.Failure("This target is already in the project scope.");
        }

        var target = new ScopeTarget
        {
            ProjectId = request.ProjectId,
            Value = value,
            Kind = request.Kind,
            IsAuthorized = request.IsAuthorized,
            Notes = request.Notes?.Trim() ?? string.Empty,
            CreatedAt = _clock.UtcNow
        };

        _db.Targets.Add(target);
        await _history.RecordAsync(request.ProjectId, ChangeAction.TargetAdded, nameof(ScopeTarget),
            $"Target '{value}' ({request.Kind}) added{(request.IsAuthorized ? ", authorized" : ", NOT authorized")}.", ct);
        await _db.SaveChangesAsync(ct);

        return Result<int>.Success(target.Id);
    }

    public async Task<Result> RemoveAsync(int targetId, CancellationToken ct = default)
    {
        var target = await _db.Targets.FirstOrDefaultAsync(t => t.Id == targetId, ct);
        if (target is null)
        {
            return Result.Failure("Target not found.");
        }

        _db.Targets.Remove(target);
        await _history.RecordAsync(target.ProjectId, ChangeAction.TargetRemoved, nameof(ScopeTarget),
            $"Target '{target.Value}' removed.", ct);
        await _db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
