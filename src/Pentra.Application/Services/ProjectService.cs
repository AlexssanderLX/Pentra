using Microsoft.EntityFrameworkCore;
using Pentra.Application.Abstractions;
using Pentra.Application.Common;
using Pentra.Application.Models;
using Pentra.Domain.Abstractions;
using Pentra.Domain.Entities;
using Pentra.Domain.Enums;

namespace Pentra.Application.Services;

public sealed class ProjectService : IProjectService
{
    private const int NameMaxLength = 200;

    private readonly IPentraDbContext _db;
    private readonly ISystemClock _clock;
    private readonly IChangeHistoryService _history;

    public ProjectService(IPentraDbContext db, ISystemClock clock, IChangeHistoryService history)
    {
        _db = db;
        _clock = clock;
        _history = history;
    }

    public async Task<IReadOnlyList<ProjectListItem>> GetListAsync(CancellationToken ct = default) =>
        await _db.Projects
            .AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectListItem(
                p.Id,
                p.Name,
                p.Client,
                p.Status,
                p.Targets.Count,
                p.ToolSelections.Count,
                p.CreatedAt))
            .ToListAsync(ct);

    public async Task<Project?> GetDetailAsync(int id, CancellationToken ct = default) =>
        await _db.Projects
            .Include(p => p.Targets)
            .Include(p => p.ToolSelections).ThenInclude(s => s.SecurityTool)
            .Include(p => p.ToolSelections).ThenInclude(s => s.Phase)
            .Include(p => p.Evidence)
            .Include(p => p.Reports)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Result<int>> CreateAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        var errors = ValidateCore(request.Name, request.Client, request.StartDate, request.EndDate);
        if (errors.Count > 0)
        {
            return Result<int>.Failure(errors);
        }

        var now = _clock.UtcNow;
        var project = new Project
        {
            Name = request.Name.Trim(),
            Client = request.Client.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            Status = request.Status,
            EngagementNotes = request.EngagementNotes?.Trim() ?? string.Empty,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CreatedAt = now
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);

        await _history.RecordAsync(project.Id, ChangeAction.Created, nameof(Project), $"Project '{project.Name}' created.", ct);
        await _db.SaveChangesAsync(ct);

        return Result<int>.Success(project.Id);
    }

    public async Task<Result> UpdateAsync(UpdateProjectRequest request, CancellationToken ct = default)
    {
        var errors = ValidateCore(request.Name, request.Client, request.StartDate, request.EndDate);
        if (errors.Count > 0)
        {
            return Result.Failure(errors);
        }

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (project is null)
        {
            return Result.Failure("Project not found.");
        }

        var statusChanged = project.Status != request.Status;
        var previousStatus = project.Status;

        project.Name = request.Name.Trim();
        project.Client = request.Client.Trim();
        project.Description = request.Description?.Trim() ?? string.Empty;
        project.Status = request.Status;
        project.EngagementNotes = request.EngagementNotes?.Trim() ?? string.Empty;
        project.StartDate = request.StartDate;
        project.EndDate = request.EndDate;
        project.UpdatedAt = _clock.UtcNow;

        await _history.RecordAsync(project.Id, ChangeAction.Updated, nameof(Project), $"Project '{project.Name}' updated.", ct);
        if (statusChanged)
        {
            await _history.RecordAsync(project.Id, ChangeAction.StatusChanged, nameof(Project),
                $"Status changed from {previousStatus} to {request.Status}.", ct);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (project is null)
        {
            return Result.Failure("Project not found.");
        }

        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static List<string> ValidateCore(string? name, string? client, DateOnly? start, DateOnly? end)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors.Add("Project name is required.");
        }
        else if (name.Trim().Length > NameMaxLength)
        {
            errors.Add($"Project name must be {NameMaxLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(client))
        {
            errors.Add("Client is required.");
        }

        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            errors.Add("End date cannot be before start date.");
        }

        return errors;
    }
}
