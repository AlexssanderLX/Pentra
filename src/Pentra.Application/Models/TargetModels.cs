using Pentra.Domain.Enums;

namespace Pentra.Application.Models;

/// <summary>Input to add an authorized scope target to a project.</summary>
public sealed record CreateTargetRequest(
    int ProjectId,
    string Value,
    TargetKind Kind,
    bool IsAuthorized,
    string Notes);
