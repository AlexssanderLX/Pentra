using Pentra.Domain.Enums;

namespace Pentra.Application.Models;

/// <summary>Input to create a new engagement.</summary>
public sealed record CreateProjectRequest(
    string Name,
    string Client,
    string Description,
    ProjectStatus Status,
    string EngagementNotes,
    DateOnly? StartDate,
    DateOnly? EndDate);

/// <summary>Input to update an existing engagement's core fields.</summary>
public sealed record UpdateProjectRequest(
    int Id,
    string Name,
    string Client,
    string Description,
    ProjectStatus Status,
    string EngagementNotes,
    DateOnly? StartDate,
    DateOnly? EndDate);

/// <summary>Lightweight projection used in listings and the dashboard.</summary>
public sealed record ProjectListItem(
    int Id,
    string Name,
    string Client,
    ProjectStatus Status,
    int TargetCount,
    int ToolCount,
    DateTime CreatedAt);
